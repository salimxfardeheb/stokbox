using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokbox.Core;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;
using Stokbox.Core.Validation;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// Cash payment of the cart: amount received, change to give back, validation of the sale.
    /// </summary>
    public sealed class PaymentViewModel : ObservableObject
    {
        private readonly SaleService _saleService;
        private readonly Cart _cart;
        private readonly long _totalCents;

        private string _receivedText;
        private string _changeCaption;
        private string _changeText;
        private bool _isShort;
        private string _error;

        public PaymentViewModel(SaleService saleService, Cart cart)
        {
            _saleService = saleService;
            _cart = cart;
            _totalCents = cart.TotalCents;

            TotalText = Money.Format(_totalCents);
            ConfirmCommand = new RelayCommand(Confirm);

            // Pre-filled with the total: paying the exact amount is a single Enter.
            _receivedText = Money.FormatForInput(_totalCents);
            UpdateChange();
        }

        /// <summary>
        /// Raised once the sale is recorded: the window closes.
        /// </summary>
        public event EventHandler Completed;

        public string TotalText { get; }

        public RelayCommand ConfirmCommand { get; }

        /// <summary>
        /// The recorded sale; null until the payment is validated.
        /// </summary>
        public Sale Sale { get; private set; }

        public string ReceivedText
        {
            get => _receivedText;
            set
            {
                if (SetProperty(ref _receivedText, value))
                {
                    Error = null;
                    UpdateChange();
                }
            }
        }

        /// <summary>
        /// "Monnaie à rendre" or "Il manque", following the amount typed.
        /// </summary>
        public string ChangeCaption
        {
            get => _changeCaption;
            private set => SetProperty(ref _changeCaption, value);
        }

        public string ChangeText
        {
            get => _changeText;
            private set => SetProperty(ref _changeText, value);
        }

        /// <summary>
        /// True while the amount typed does not cover the total (RG-08).
        /// </summary>
        public bool IsShort
        {
            get => _isShort;
            private set => SetProperty(ref _isShort, value);
        }

        public string Error
        {
            get => _error;
            private set => SetProperty(ref _error, value);
        }

        private void UpdateChange()
        {
            if (!TryReadReceived(out var receivedCents))
            {
                ChangeCaption = "Monnaie à rendre";
                ChangeText = "—";
                IsShort = true;
                return;
            }

            IsShort = receivedCents < _totalCents;
            ChangeCaption = IsShort ? "Il manque" : "Monnaie à rendre";
            ChangeText = Money.Format(Math.Abs(receivedCents - _totalCents));
        }

        private bool TryReadReceived(out long receivedCents)
        {
            receivedCents = 0;
            if (!Money.TryParse(ReceivedText, out var amount)
                || amount < 0
                || amount > ProductService.MaxPrice * 1000
                || !Money.HasAtMostTwoDecimals(amount))
            {
                return false;
            }

            receivedCents = Money.ToCents(amount);
            return true;
        }

        private void Confirm()
        {
            if (!TryReadReceived(out var receivedCents))
            {
                Error = "Montant invalide. Exemples : 1500 ou 1500,50.";
                return;
            }

            try
            {
                Sale = _saleService.Validate(_cart, receivedCents);
            }
            catch (BusinessRuleException ex)
            {
                // Not enough cash, or a product that ran out or was archived meanwhile: nothing was recorded.
                Error = ex.Message;
                return;
            }

            Completed?.Invoke(this, EventArgs.Empty);
        }
    }
}
