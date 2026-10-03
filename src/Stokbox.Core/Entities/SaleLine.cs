namespace Stokbox.Core.Entities
{
    /// <summary>
    /// One product of a sale, with the prices frozen at the time of the sale (RG-04).
    /// </summary>
    public sealed class SaleLine
    {
        public long Id { get; set; }

        public long ProductId { get; set; }

        public string ProductName { get; set; }

        public int Quantity { get; set; }

        public long UnitPriceCents { get; set; }

        public long UnitPurchasePriceCents { get; set; }

        public long LineTotalCents { get; set; }

        /// <summary>
        /// Quantity already brought back by the customer, all returns together.
        /// </summary>
        public int ReturnedQuantity { get; set; }

        /// <summary>
        /// Quantity that can still be returned (RG-06).
        /// </summary>
        public int ReturnableQuantity => Quantity - ReturnedQuantity;
    }
}
