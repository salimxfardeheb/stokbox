using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Windows.Controls;
using Stokbox.App.Services;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;

namespace Stokbox.App.Printing
{
    /// <summary>
    /// Prints price labels through the Windows driver of any printer.
    /// </summary>
    public sealed class LabelPrintService : ILabelPrintService
    {
        /// <summary>
        /// Highest number of labels in one print job.
        /// </summary>
        public const int MaxLabelsPerJob = 2000;

        private readonly LabelSettingsService _settingsService;
        private readonly IDialogService _dialogs;
        private readonly IErrorLog _errorLog;

        public LabelPrintService(LabelSettingsService settingsService, IDialogService dialogs, IErrorLog errorLog)
        {
            _settingsService = settingsService;
            _dialogs = dialogs;
            _errorLog = errorLog;
        }

        public bool Print(IReadOnlyList<LabelPrintItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return false;
            }

            if (items.Sum(item => (long)item.Copies) > MaxLabelsPerJob)
            {
                _dialogs.ShowWarning("Trop d'étiquettes pour une seule impression (maximum : 2 000). Réduisez le nombre d'exemplaires.");
                return false;
            }

            return PrintLabels(LabelDocumentBuilder.Expand(items), _settingsService.Get(), "Stokbox - Étiquettes");
        }

        public bool PrintTestLabel(LabelSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            // A full page, so that every position of a sheet can be checked against the paper.
            var labels = Enumerable.Repeat(LabelContent.CreateSample(), settings.LabelsPerPage).ToList();
            return PrintLabels(labels, settings, "Stokbox - Étiquette de test");
        }

        private bool PrintLabels(IReadOnlyList<LabelContent> labels, LabelSettings settings, string jobName)
        {
            try
            {
                var dialog = new PrintDialog();

                // A saved printer prints straight away; otherwise the Windows dialog asks which one.
                var queue = WindowsPrinters.FindQueue(settings.PrinterName);
                if (queue != null)
                {
                    dialog.PrintQueue = queue;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(settings.PrinterName))
                    {
                        _dialogs.ShowWarning(
                            "L'imprimante « " + settings.PrinterName + " » est introuvable. Choisissez une autre imprimante.");
                    }

                    if (dialog.ShowDialog() != true)
                    {
                        return false;
                    }
                }

                var document = LabelDocumentBuilder.CreateDocument(labels, settings, ReadDeviceDpi(dialog.PrintTicket));
                dialog.PrintDocument(document.DocumentPaginator, jobName);
                return true;
            }
            catch (Exception ex)
            {
                _errorLog.Write("Impression d'étiquettes", ex);
                _dialogs.ShowWarning(
                    "L'impression a échoué. Vérifiez que l'imprimante est allumée et connectée."
                    + Environment.NewLine + Environment.NewLine
                    + "Le détail a été enregistré dans : " + _errorLog.LogsDirectory);
                return false;
            }
        }

        private static double ReadDeviceDpi(PrintTicket ticket)
        {
            var dpi = ticket?.PageResolution?.X;
            return dpi.HasValue && dpi.Value > 0 ? dpi.Value : LabelElement.DefaultDeviceDpi;
        }
    }
}
