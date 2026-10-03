using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokbox.App.Services;
using Stokbox.Core;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;
using Stokbox.Core.Validation;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// Counter sale, keyboard and scanner only: scan the articles, F2, Enter.
    /// </summary>
    public sealed class SaleViewModel : ObservableObject
    {
        private readonly SaleService _saleService;
        private readonly IReceiptPrintService _receiptPrintService;
        private readonly IDialogService _dialogs;
        private readonly Cart _cart = new Cart();

        private CartLineRow _selectedLine;
        private string _message;
        private string _lastSaleText;

        public SaleViewModel(
            SaleService saleService,
            IReceiptPrintService receiptPrintService,
            IDialogService dialogs,
            ProductSearchViewModel search)
        {
            _saleService = saleService;
            _receiptPrintService = receiptPrintService;
            _dialogs = dialogs;

            Search = search;
            Search.ProductSelected += (sender, product) => AddProduct(product);

            Lines = new ObservableCollection<CartLineRow>();
            CheckoutCommand = new RelayCommand(Checkout);
            ClearCartCommand = new RelayCommand(ClearCart);
            RemoveSelectedCommand = new RelayCommand(RemoveSelected);
            IncreaseCommand = new RelayCommand(() => ChangeQuantity(1));
            DecreaseCommand = new RelayCommand(() => ChangeQuantity(-1));
        }

        public ProductSearchViewModel Search { get; }

        public ObservableCollection<CartLineRow> Lines { get; }

        public RelayCommand CheckoutCommand { get; }

        public RelayCommand ClearCartCommand { get; }

        public RelayCommand RemoveSelectedCommand { get; }

        public RelayCommand IncreaseCommand { get; }

        public RelayCommand DecreaseCommand { get; }

        /// <summary>
        /// Line the quantity and removal shortcuts apply to.
        /// </summary>
        public CartLineRow SelectedLine
        {
            get => _selectedLine;
            set => SetProperty(ref _selectedLine, value);
        }

        public string TotalText => Money.Format(_cart.TotalCents);

        public string ArticleCountText
        {
            get
            {
                var count = _cart.ArticleCount;
                return count.ToString(CultureInfo.InvariantCulture) + (count > 1 ? " articles" : " article");
            }
        }

        /// <summary>
        /// Why the last action was refused; null otherwise.
        /// </summary>
        public string Message
        {
            get => _message;
            private set => SetProperty(ref _message, value);
        }

        /// <summary>
        /// Reminder of the sale just recorded, with the change to give back.
        /// </summary>
        public string LastSaleText
        {
            get => _lastSaleText;
            private set
            {
                if (SetProperty(ref _lastSaleText, value))
                {
                    OnPropertyChanged(nameof(HasLastSale));
                }
            }
        }

        public bool HasLastSale => _lastSaleText != null;

        /// <summary>
        /// Called each time the screen is shown: prices or stock may have changed in another screen.
        /// </summary>
        public void Activate()
        {
            Message = null;
            if (!_cart.IsEmpty && _saleService.Refresh(_cart))
            {
                Message = "Le panier a été mis à jour : des prix ou des stocks ont changé.";
            }

            ShowCart(SelectedLine?.ProductId);
        }

        /// <summary>
        /// Up and down arrows: moves the selected line.
        /// </summary>
        public void MoveSelection(int offset)
        {
            if (Lines.Count == 0)
            {
                return;
            }

            var index = Lines.IndexOf(SelectedLine) + offset;
            SelectedLine = Lines[Math.Max(0, Math.Min(Lines.Count - 1, index))];
        }

        private void AddProduct(Product product)
        {
            LastSaleText = null;
            Run(() => _saleService.AddProduct(_cart, product.Id));
            ShowCart(product.Id);
            Search.RequestFocus();
        }

        private void ChangeQuantity(int offset)
        {
            var line = SelectedLine;
            if (line == null || line.Quantity + offset < 1)
            {
                return;
            }

            Run(() => _saleService.SetQuantity(_cart, line.ProductId, line.Quantity + offset));
            ShowCart(line.ProductId);
        }

        private void OnQuantityTyped(CartLineRow line, string text)
        {
            if (!int.TryParse((text ?? string.Empty).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var quantity))
            {
                Message = "Quantité invalide : saisissez un nombre entier, par exemple 2.";
            }
            else
            {
                Run(() => _saleService.SetQuantity(_cart, line.ProductId, quantity));
            }

            ShowCart(line.ProductId);
        }

        private void RemoveSelected()
        {
            var line = SelectedLine;
            if (line == null)
            {
                return;
            }

            // The selection moves to the neighbouring line, so that several lines can be removed in a row.
            var index = Lines.IndexOf(line);
            Message = null;
            _saleService.RemoveLine(_cart, line.ProductId);
            ShowCart(null);
            if (Lines.Count > 0)
            {
                SelectedLine = Lines[Math.Min(index, Lines.Count - 1)];
            }
        }

        private void ClearCart()
        {
            if (_cart.IsEmpty)
            {
                return;
            }

            if (_dialogs.Confirm("Vider le panier ?"))
            {
                Message = null;
                _cart.Clear();
                ShowCart(null);
            }

            Search.RequestFocus();
        }

        private void Checkout()
        {
            if (_cart.IsEmpty)
            {
                Message = "Le panier est vide.";
                return;
            }

            Message = null;
            var payment = new PaymentViewModel(_saleService, _cart);
            if (_dialogs.ShowPayment(payment))
            {
                var sale = payment.Sale;
                ShowCart(null);
                LastSaleText = "Vente " + sale.Number + " enregistrée — total " + Money.Format(sale.TotalCents)
                    + ", reçu " + Money.Format(sale.ReceivedCents)
                    + ", monnaie rendue : " + Money.Format(sale.ChangeCents);

                if (_receiptPrintService.IsEnabled
                    && _dialogs.Ask(
                        "Monnaie à rendre : " + Money.Format(sale.ChangeCents),
                        "Imprimer le ticket ?"))
                {
                    _receiptPrintService.Print(sale);
                }
            }
            else
            {
                // The payment may have been refused because a product ran out or was archived: realign the cart.
                if (_saleService.Refresh(_cart))
                {
                    Message = "Le panier a été mis à jour : des prix ou des stocks ont changé.";
                }

                ShowCart(SelectedLine?.ProductId);
            }

            Search.Clear();
            Search.RequestFocus();
        }

        // A refused action explains itself under the search field and leaves the cart as it was.
        private void Run(Action action)
        {
            try
            {
                action();
                Message = null;
            }
            catch (BusinessRuleException ex)
            {
                Message = ex.Message;
            }
        }

        // Rows are updated in place: the grid keeps its selection and the cell being edited is not torn down.
        private void ShowCart(long? productIdToSelect)
        {
            foreach (var row in Lines.Where(row => _cart.Find(row.ProductId) == null).ToList())
            {
                Lines.Remove(row);
            }

            foreach (var line in _cart.Lines)
            {
                var row = Lines.FirstOrDefault(r => r.ProductId == line.ProductId);
                if (row == null)
                {
                    Lines.Add(new CartLineRow(line, OnQuantityTyped));
                }
                else
                {
                    row.Update(line);
                }
            }

            SelectedLine = Lines.FirstOrDefault(row => row.ProductId == productIdToSelect) ?? Lines.LastOrDefault();

            OnPropertyChanged(nameof(TotalText));
            OnPropertyChanged(nameof(ArticleCountText));
        }
    }
}
