using System;
using System.Printing;
using System.Windows.Controls;
using Stokbox.App.Services;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;

namespace Stokbox.App.Printing
{
    /// <summary>
    /// Prints receipts on the receipt printer of the settings, through its Windows driver, without any dialog.
    /// </summary>
    public sealed class ReceiptPrintService : IReceiptPrintService
    {
        private readonly ReceiptSettingsService _settingsService;
        private readonly IDialogService _dialogs;
        private readonly IErrorLog _errorLog;

        public ReceiptPrintService(ReceiptSettingsService settingsService, IDialogService dialogs, IErrorLog errorLog)
        {
            _settingsService = settingsService;
            _dialogs = dialogs;
            _errorLog = errorLog;
        }

        public bool IsEnabled => _settingsService.Get().PrinterName != null;

        public bool Print(Sale sale)
        {
            if (sale == null)
            {
                throw new ArgumentNullException(nameof(sale));
            }

            return Print(sale, _settingsService.Get(), "Stokbox - Ticket " + sale.Number);
        }

        public bool PrintTest(ReceiptSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return Print(ReceiptDocumentBuilder.CreateSampleSale(), settings, "Stokbox - Ticket de test");
        }

        private bool Print(Sale sale, ReceiptSettings settings, string jobName)
        {
            if (string.IsNullOrWhiteSpace(settings.PrinterName))
            {
                return false;
            }

            try
            {
                var queue = WindowsPrinters.FindQueue(settings.PrinterName);
                if (queue == null)
                {
                    _dialogs.ShowWarning(
                        "L'imprimante ticket « " + settings.PrinterName + " » est introuvable. Vérifiez-la dans Paramètres.");
                    return false;
                }

                var dialog = new PrintDialog { PrintQueue = queue };

                double left = 0;
                double top = 0;
                var width = Units.MmToDip(ReceiptDocumentBuilder.PrintableWidthMm);
                ReadPrintableArea(queue, dialog.PrintTicket, ref left, ref top, ref width);

                var document = ReceiptDocumentBuilder.CreateDocument(sale, settings, left, top, width);
                dialog.PrintDocument(document.DocumentPaginator, jobName);
                return true;
            }
            catch (Exception ex)
            {
                _errorLog.Write("Impression du ticket", ex);
                _dialogs.ShowWarning(
                    "L'impression du ticket a échoué. Vérifiez que l'imprimante est allumée et connectée."
                    + Environment.NewLine + Environment.NewLine
                    + "Le détail a été enregistré dans : " + _errorLog.LogsDirectory);
                return false;
            }
        }

        // The driver knows where the printable band of the paper starts and how wide it is;
        // the receipt uses it, without ever exceeding 72 mm. Left as is when the driver does not say.
        private static void ReadPrintableArea(PrintQueue queue, PrintTicket ticket, ref double left, ref double top, ref double width)
        {
            try
            {
                var area = queue.GetPrintCapabilities(ticket).PageImageableArea;
                if (area != null && area.ExtentWidth >= Units.MmToDip(40.0))
                {
                    left = area.OriginWidth;
                    top = area.OriginHeight;
                    width = Math.Min(width, area.ExtentWidth);
                }
            }
            catch (Exception)
            {
                // Some drivers cannot report their capabilities: the defaults suit a receipt printer.
            }
        }
    }
}
