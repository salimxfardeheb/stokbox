using System;

namespace Stokbox.Core.Entities
{
    public sealed class Product
    {
        public long Id { get; set; }

        /// <summary>
        /// Internal EAN-13, assigned at creation and never changed nor reused (RG-02).
        /// </summary>
        public string Barcode { get; set; }

        public string Name { get; set; }

        public long? CategoryId { get; set; }

        public string CategoryName { get; set; }

        public long PurchasePriceCents { get; set; }

        public long SalePriceCents { get; set; }

        public bool IsArchived { get; set; }

        /// <summary>
        /// Sum of the stock movements of the product (RG-01); never stored.
        /// </summary>
        public long StockQuantity { get; set; }

        public long MarginCents => SalePriceCents - PurchasePriceCents;

        /// <summary>
        /// Margin relative to the purchase price, in percent; null when the purchase price is 0.
        /// </summary>
        public decimal? MarginPercent
        {
            get
            {
                if (PurchasePriceCents <= 0)
                {
                    return null;
                }

                return Math.Round(MarginCents * 100m / PurchasePriceCents, 1, MidpointRounding.AwayFromZero);
            }
        }
    }
}
