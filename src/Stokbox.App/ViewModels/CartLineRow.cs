using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Stokbox.Core.Entities;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// One line of the cart as shown in the sale screen.
    /// </summary>
    public sealed class CartLineRow : ObservableObject
    {
        private readonly Action<CartLineRow, string> _onQuantityTyped;
        private CartLine _line;

        public CartLineRow(CartLine line, Action<CartLineRow, string> onQuantityTyped)
        {
            _line = line;
            _onQuantityTyped = onQuantityTyped;
        }

        public long ProductId => _line.ProductId;

        public string Name => _line.Name;

        public long UnitPriceCents => _line.UnitPriceCents;

        public int Quantity => _line.Quantity;

        public long LineTotalCents => _line.LineTotalCents;

        /// <summary>
        /// Quantity as typed in the grid. A refused value leaves the cart as it was and the text comes back.
        /// </summary>
        public string QuantityText
        {
            get => _line.Quantity.ToString(CultureInfo.InvariantCulture);
            set => _onQuantityTyped(this, value);
        }

        /// <summary>
        /// Shows the line as it is now in the cart.
        /// </summary>
        public void Update(CartLine line)
        {
            _line = line;
            OnPropertyChanged(string.Empty);
        }
    }
}
