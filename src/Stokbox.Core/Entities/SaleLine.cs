namespace Stokbox.Core.Entities
{
    /// <summary>
    /// One product of a sale, with the prices frozen at the time of the sale (RG-04).
    /// </summary>
    public sealed class SaleLine
    {
        public long ProductId { get; set; }

        public string ProductName { get; set; }

        public int Quantity { get; set; }

        public long UnitPriceCents { get; set; }

        public long UnitPurchasePriceCents { get; set; }

        public long LineTotalCents { get; set; }
    }
}
