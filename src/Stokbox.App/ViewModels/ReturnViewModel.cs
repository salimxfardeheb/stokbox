using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Stokbox.Core;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;
using Stokbox.Core.Validation;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// Return of articles of one sale: a quantity per line, and the amount to give back.
    /// </summary>
    public sealed class ReturnViewModel : ObservableObject
    {
        private readonly SaleService _saleService;
        private readonly long _saleId;

        private string _refundText;
        private string _error;

        public ReturnViewModel(SaleService saleService, Sale sale)
        {
            _saleService = saleService;
            _saleId = sale.Id;

            Title = "Retour d'articles — vente " + sale.Number;
            Rows = sale.Lines
                .Where(line => line.ReturnableQuantity > 0)
                .Select(line => new ReturnLineRow(line, OnQuantityChanged))
                .ToList();

            ConfirmCommand = new RelayCommand(Confirm);
            ReturnAllCommand = new RelayCommand(ReturnAll);
            UpdateRefund();
        }

        /// <summary>
        /// Raised once the return is recorded: the window closes.
        /// </summary>
        public event EventHandler Completed;

        public string Title { get; }

        /// <summary>
        /// The lines that can still be returned.
        /// </summary>
        public IReadOnlyList<ReturnLineRow> Rows { get; }

        public RelayCommand ConfirmCommand { get; }

        public RelayCommand ReturnAllCommand { get; }

        /// <summary>
        /// The recorded return; null until it is validated.
        /// </summary>
        public SaleReturn Result { get; private set; }

        /// <summary>
        /// Amount to give back for the quantities typed, at the prices frozen on the sale.
        /// </summary>
        public string RefundText
        {
            get => _refundText;
            private set => SetProperty(ref _refundText, value);
        }

        public string Error
        {
            get => _error;
            private set => SetProperty(ref _error, value);
        }

        private void OnQuantityChanged()
        {
            Error = null;
            UpdateRefund();
        }

        private void UpdateRefund()
        {
            RefundText = Money.Format(Rows.Sum(row => (row.Quantity ?? 0) * row.UnitPriceCents));
        }

        private void ReturnAll()
        {
            foreach (var row in Rows)
            {
                row.QuantityText = row.ReturnableQuantity.ToString(CultureInfo.InvariantCulture);
            }
        }

        private void Confirm()
        {
            var invalid = Rows.FirstOrDefault(row => row.HasError);
            if (invalid != null)
            {
                Error = "Quantité invalide pour « " + invalid.Name + " » : saisissez un entier de 0 à "
                    + invalid.ReturnableQuantity.ToString(CultureInfo.InvariantCulture) + ".";
                return;
            }

            var lines = Rows
                .Where(row => row.Quantity > 0)
                .Select(row => new ReturnRequestLine(row.SaleLineId, row.Quantity.Value))
                .ToList();
            if (lines.Count == 0)
            {
                Error = "Indiquez la quantité retournée d'au moins un article.";
                return;
            }

            try
            {
                Result = _saleService.ReturnItems(_saleId, lines);
            }
            catch (BusinessRuleException ex)
            {
                // The sale was cancelled or returned meanwhile: nothing was recorded.
                Error = ex.Message;
                return;
            }

            Completed?.Invoke(this, EventArgs.Empty);
        }
    }
}
