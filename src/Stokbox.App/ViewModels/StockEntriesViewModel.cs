using System;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;
using Stokbox.Core.Validation;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// Stock entries, keyboard and scanner only: pick a product, type the quantity, Enter, and again.
    /// </summary>
    public sealed class StockEntriesViewModel : ObservableObject
    {
        private readonly StockService _stockService;
        private readonly ILabelPrintService _labelPrintService;

        private Product _selectedProduct;
        private string _quantityText = string.Empty;
        private string _quantityError;
        private StockEntry _lastEntry;

        public StockEntriesViewModel(
            StockService stockService,
            ILabelPrintService labelPrintService,
            ProductSearchViewModel search)
        {
            _stockService = stockService;
            _labelPrintService = labelPrintService;

            Search = search;
            Search.ProductSelected += (sender, product) => SelectProduct(product);

            Entries = new ObservableCollection<StockEntryRow>();
            ConfirmCommand = new RelayCommand(Confirm, () => SelectedProduct != null);
            CancelCommand = new RelayCommand(Cancel);
            PrintLabelsCommand = new RelayCommand(PrintLabels, () => _lastEntry != null);
        }

        /// <summary>
        /// Asks the view to move the keyboard focus to the quantity field.
        /// </summary>
        public event EventHandler QuantityFocusRequested;

        public ProductSearchViewModel Search { get; }

        /// <summary>
        /// Entries recorded since the application started, the latest first.
        /// </summary>
        public ObservableCollection<StockEntryRow> Entries { get; }

        public RelayCommand ConfirmCommand { get; }

        public RelayCommand CancelCommand { get; }

        public RelayCommand PrintLabelsCommand { get; }

        public Product SelectedProduct
        {
            get => _selectedProduct;
            private set
            {
                if (SetProperty(ref _selectedProduct, value))
                {
                    OnPropertyChanged(nameof(HasSelectedProduct));
                    ConfirmCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HasSelectedProduct => SelectedProduct != null;

        public string QuantityText
        {
            get => _quantityText;
            set
            {
                if (SetProperty(ref _quantityText, value))
                {
                    QuantityError = null;
                }
            }
        }

        public string QuantityError
        {
            get => _quantityError;
            private set => SetProperty(ref _quantityError, value);
        }

        public bool HasLastEntry => _lastEntry != null;

        /// <summary>
        /// Confirmation of the latest entry, e.g. "12 × Café moulu ajoutés — stock : 20".
        /// </summary>
        public string LastEntryText
        {
            get
            {
                if (_lastEntry == null)
                {
                    return null;
                }

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Entrée enregistrée : {0} × {1} — stock : {2}",
                    _lastEntry.Quantity,
                    _lastEntry.Product.Name,
                    _lastEntry.StockQuantityAfter);
            }
        }

        public string PrintLabelsText
        {
            get
            {
                if (_lastEntry == null)
                {
                    return null;
                }

                return _lastEntry.Quantity == 1
                    ? "Imprimer 1 étiquette ? (F6)"
                    : "Imprimer " + _lastEntry.Quantity.ToString(CultureInfo.InvariantCulture) + " étiquettes ? (F6)";
            }
        }

        private void SelectProduct(Product product)
        {
            SelectedProduct = product;
            QuantityText = string.Empty;
            QuantityError = null;
            QuantityFocusRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Confirm()
        {
            var product = SelectedProduct;
            if (product == null)
            {
                return;
            }

            // NumberStyles.None: digits only, so "1,5", "-3" or "1e3" are not quantities.
            if (!int.TryParse((QuantityText ?? string.Empty).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var quantity))
            {
                QuantityError = "Saisissez une quantité entière, par exemple 12.";
                QuantityFocusRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            StockEntry entry;
            try
            {
                entry = _stockService.AddEntry(product.Id, quantity);
            }
            catch (BusinessRuleException ex)
            {
                QuantityError = ex.Message;
                QuantityFocusRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            Entries.Insert(0, new StockEntryRow(entry));
            SetLastEntry(entry);
            ReturnToSearch();
        }

        private void Cancel()
        {
            ReturnToSearch();
        }

        private void ReturnToSearch()
        {
            SelectedProduct = null;
            QuantityText = string.Empty;
            QuantityError = null;
            Search.Clear();
            Search.RequestFocus();
        }

        private void PrintLabels()
        {
            if (_lastEntry == null)
            {
                return;
            }

            _labelPrintService.PrintLabels(_lastEntry.Product, _lastEntry.Quantity);
            if (!HasSelectedProduct)
            {
                Search.RequestFocus();
            }
        }

        private void SetLastEntry(StockEntry entry)
        {
            _lastEntry = entry;
            OnPropertyChanged(nameof(HasLastEntry));
            OnPropertyChanged(nameof(LastEntryText));
            OnPropertyChanged(nameof(PrintLabelsText));
            PrintLabelsCommand.NotifyCanExecuteChanged();
        }
    }
}
