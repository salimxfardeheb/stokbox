using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokbox.App.Converters;
using Stokbox.App.Services;
using Stokbox.Core;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;
using Stokbox.Core.Validation;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// History screen: the sales of a period, the detail of one, its cancellation, its returns, its receipt.
    /// </summary>
    public sealed class HistoryViewModel : ObservableObject
    {
        private readonly SaleService _saleService;
        private readonly IReceiptPrintService _receiptPrintService;
        private readonly IDialogService _dialogs;

        private DateTime? _fromDate = DateTime.Today;
        private DateTime? _toDate = DateTime.Today;
        private string _numberText = string.Empty;
        private SaleSummary _selectedSale;
        private Sale _details;
        private bool _hasReturns;
        private string _summaryText;
        private string _detailTitle;
        private string _paymentText;
        private string _refundedText;
        private string _statusMessage;

        // Set while the list is rebuilt: the binding then pushes a transient null selection.
        private bool _isReloading;

        public HistoryViewModel(SaleService saleService, IReceiptPrintService receiptPrintService, IDialogService dialogs)
        {
            _saleService = saleService;
            _receiptPrintService = receiptPrintService;
            _dialogs = dialogs;

            Sales = new ObservableCollection<SaleSummary>();
            Lines = new ObservableCollection<SaleLine>();
            ReturnRows = new ObservableCollection<HistoryReturnRow>();

            TodayCommand = new RelayCommand(ShowToday);
            CancelSaleCommand = new RelayCommand(CancelSale, () => CanCancel);
            ReturnItemsCommand = new RelayCommand(ReturnItems, () => CanReturn);
            ReprintCommand = new RelayCommand(Reprint, () => _details != null);
        }

        public ObservableCollection<SaleSummary> Sales { get; }

        /// <summary>
        /// Lines of the selected sale.
        /// </summary>
        public ObservableCollection<SaleLine> Lines { get; }

        /// <summary>
        /// Articles already returned on the selected sale.
        /// </summary>
        public ObservableCollection<HistoryReturnRow> ReturnRows { get; }

        public RelayCommand TodayCommand { get; }

        public RelayCommand CancelSaleCommand { get; }

        public RelayCommand ReturnItemsCommand { get; }

        public RelayCommand ReprintCommand { get; }

        public DateTime? FromDate
        {
            get => _fromDate;
            set
            {
                if (SetProperty(ref _fromDate, value))
                {
                    Reload();
                }
            }
        }

        public DateTime? ToDate
        {
            get => _toDate;
            set
            {
                if (SetProperty(ref _toDate, value))
                {
                    Reload();
                }
            }
        }

        /// <summary>
        /// Sale number, or part of one; when given, every period is searched.
        /// </summary>
        public string NumberText
        {
            get => _numberText;
            set
            {
                if (SetProperty(ref _numberText, value))
                {
                    Reload();
                }
            }
        }

        public SaleSummary SelectedSale
        {
            get => _selectedSale;
            set
            {
                if (_isReloading)
                {
                    return;
                }

                if (SetProperty(ref _selectedSale, value))
                {
                    StatusMessage = null;
                    LoadDetails();
                }
            }
        }

        public bool HasSelection => _details != null;

        public bool HasReturns => _hasReturns;

        public string SummaryText
        {
            get => _summaryText;
            private set => SetProperty(ref _summaryText, value);
        }

        public string DetailTitle
        {
            get => _detailTitle;
            private set => SetProperty(ref _detailTitle, value);
        }

        public string PaymentText
        {
            get => _paymentText;
            private set => SetProperty(ref _paymentText, value);
        }

        public string RefundedText
        {
            get => _refundedText;
            private set => SetProperty(ref _refundedText, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        // RG-05: a sale is cancelled only while validated and without any return.
        private bool CanCancel => _details != null && _details.Status == Sale.StatusValidated && !_hasReturns;

        private bool CanReturn => _details != null
            && _details.Status == Sale.StatusValidated
            && _details.Lines.Any(line => line.ReturnableQuantity > 0);

        /// <summary>
        /// Reads the sales again; called each time the screen is shown.
        /// </summary>
        public void Refresh()
        {
            StatusMessage = null;
            Reload();
        }

        private void ShowToday()
        {
            _fromDate = DateTime.Today;
            _toDate = DateTime.Today;
            _numberText = string.Empty;
            OnPropertyChanged(nameof(FromDate));
            OnPropertyChanged(nameof(ToDate));
            OnPropertyChanged(nameof(NumberText));
            Reload();
        }

        private void Reload()
        {
            var selectedId = _selectedSale?.Id;

            // An emptied date field means "today".
            var found = _saleService.SearchSales(FromDate ?? DateTime.Today, ToDate ?? DateTime.Today, NumberText);

            _isReloading = true;
            try
            {
                Sales.Clear();
                foreach (var sale in found)
                {
                    Sales.Add(sale);
                }
            }
            finally
            {
                _isReloading = false;
            }

            var validated = found.Where(sale => sale.Status == Sale.StatusValidated).ToList();
            SummaryText = found.Count == 0
                ? "Aucune vente."
                : Plural(found.Count, "vente", "ventes") + " — dont " + Plural(validated.Count, "validée", "validées")
                    + " pour " + Money.Format(validated.Sum(sale => sale.TotalCents));

            _selectedSale = Sales.FirstOrDefault(sale => sale.Id == selectedId);
            OnPropertyChanged(nameof(SelectedSale));
            LoadDetails();
        }

        private void LoadDetails()
        {
            _details = _selectedSale == null ? null : _saleService.GetSale(_selectedSale.Id);

            Lines.Clear();
            ReturnRows.Clear();
            _hasReturns = false;

            if (_details == null)
            {
                DetailTitle = null;
                PaymentText = null;
                RefundedText = null;
            }
            else
            {
                foreach (var line in _details.Lines)
                {
                    Lines.Add(line);
                }

                var returns = _saleService.GetReturns(_details.Id);
                foreach (var saleReturn in returns)
                {
                    foreach (var line in saleReturn.Lines)
                    {
                        ReturnRows.Add(new HistoryReturnRow(saleReturn, line));
                    }
                }

                _hasReturns = returns.Count > 0;
                DetailTitle = "Vente " + _details.Number + " — " + LocalDateTimeConverter.Format(_details.CreatedAtUtc)
                    + " — " + SaleStatusConverter.Format(_details.Status);
                PaymentText = "Total " + Money.Format(_details.TotalCents)
                    + " — reçu " + Money.Format(_details.ReceivedCents)
                    + " — rendu " + Money.Format(_details.ChangeCents);
                RefundedText = _hasReturns
                    ? "Retours : " + Money.Format(returns.Sum(r => r.RefundCents)) + " remboursés"
                    : null;
            }

            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(HasReturns));
            CancelSaleCommand.NotifyCanExecuteChanged();
            ReturnItemsCommand.NotifyCanExecuteChanged();
            ReprintCommand.NotifyCanExecuteChanged();
        }

        private void CancelSale()
        {
            var sale = _details;
            if (sale == null)
            {
                return;
            }

            var confirmed = _dialogs.Confirm(
                "Annuler la vente " + sale.Number + " ?"
                + Environment.NewLine + Environment.NewLine
                + "Tous ses articles seront remis en stock et " + Money.Format(sale.TotalCents)
                + " sont à rembourser au client. Une vente annulée ne peut pas être rétablie.");
            if (!confirmed)
            {
                return;
            }

            string message = null;
            try
            {
                _saleService.CancelSale(sale.Id);
                message = "Vente " + sale.Number + " annulée : articles remis en stock, "
                    + Money.Format(sale.TotalCents) + " à rembourser au client.";
            }
            catch (BusinessRuleException ex)
            {
                _dialogs.ShowWarning(ex.Message);
            }

            Reload();
            StatusMessage = message;
        }

        private void ReturnItems()
        {
            // Read again: the quantities still returnable must be the current ones.
            var sale = _details == null ? null : _saleService.GetSale(_details.Id);
            if (sale == null)
            {
                return;
            }

            var form = new ReturnViewModel(_saleService, sale);
            string message = null;
            if (_dialogs.ShowReturn(form))
            {
                message = "Retour enregistré sur la vente " + sale.Number + " : "
                    + Money.Format(form.Result.RefundCents) + " à rembourser au client.";
            }

            Reload();
            StatusMessage = message;
        }

        private void Reprint()
        {
            if (_details == null)
            {
                return;
            }

            if (!_receiptPrintService.IsEnabled)
            {
                _dialogs.ShowWarning("Aucune imprimante ticket n'est choisie. Réglez-la dans Paramètres > Boutique et ticket.");
                return;
            }

            StatusMessage = _receiptPrintService.Print(_details)
                ? "Ticket de la vente " + _details.Number + " envoyé à l'imprimante."
                : null;
        }

        private static string Plural(int count, string singular, string plural)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + (count > 1 ? plural : singular);
        }
    }
}
