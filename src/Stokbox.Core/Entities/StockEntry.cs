using System;

namespace Stokbox.Core.Entities
{
    /// <summary>
    /// A stock entry that has just been recorded.
    /// </summary>
    public sealed class StockEntry
    {
        public StockEntry(Product product, int quantity, DateTime createdAtUtc)
        {
            Product = product;
            Quantity = quantity;
            CreatedAtUtc = createdAtUtc;
        }

        /// <summary>
        /// The product as it was just before the entry.
        /// </summary>
        public Product Product { get; }

        public int Quantity { get; }

        public DateTime CreatedAtUtc { get; }

        public long StockQuantityAfter => Product.StockQuantity + Quantity;
    }
}
