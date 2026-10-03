using System.Globalization;
using Stokbox.Core.Entities;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// One line of the list of entries recorded since the application started.
    /// </summary>
    public sealed class StockEntryRow
    {
        public StockEntryRow(StockEntry entry)
        {
            ProductName = entry.Product.Name;
            Barcode = entry.Product.Barcode;
            Quantity = entry.Quantity;
            StockQuantityAfter = entry.StockQuantityAfter;
            Time = entry.CreatedAtUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        public string ProductName { get; }

        public string Barcode { get; }

        public int Quantity { get; }

        public long StockQuantityAfter { get; }

        /// <summary>
        /// Local time of the entry.
        /// </summary>
        public string Time { get; }
    }
}
