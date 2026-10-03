using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Stokbox.Core.Entities;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// One line of a sale in the return window: how many can come back, how many do.
    /// </summary>
    public sealed class ReturnLineRow : ObservableObject
    {
        private readonly Action _onChanged;
        private string _quantityText = "0";

        public ReturnLineRow(SaleLine line, Action onChanged)
        {
            SaleLineId = line.Id;
            Name = line.ProductName;
            UnitPriceCents = line.UnitPriceCents;
            SoldQuantity = line.Quantity;
            ReturnedQuantity = line.ReturnedQuantity;
            ReturnableQuantity = line.ReturnableQuantity;
            _onChanged = onChanged;
        }

        public long SaleLineId { get; }

        public string Name { get; }

        public long UnitPriceCents { get; }

        public int SoldQuantity { get; }

        public int ReturnedQuantity { get; }

        public int ReturnableQuantity { get; }

        public string QuantityText
        {
            get => _quantityText;
            set
            {
                if (SetProperty(ref _quantityText, value))
                {
                    OnPropertyChanged(nameof(HasError));
                    _onChanged();
                }
            }
        }

        /// <summary>
        /// Quantity to return; an empty field means none. Null when the text is not an integer
        /// between 0 and the returnable quantity (RG-06).
        /// </summary>
        public int? Quantity
        {
            get
            {
                var text = (QuantityText ?? string.Empty).Trim();
                if (text.Length == 0)
                {
                    return 0;
                }

                return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity)
                    && quantity <= ReturnableQuantity
                        ? quantity
                        : (int?)null;
            }
        }

        public bool HasError => Quantity == null;
    }
}
