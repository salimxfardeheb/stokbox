namespace Stokbox.Core.Dashboard
{
    /// <summary>
    /// Value of the stock right now. Only the products with a quantity above 0 are valued, archived or not.
    /// </summary>
    public sealed class StockSummary
    {
        public StockSummary(long purchaseValueCents, long saleValueCents, long referenceCount, long unitCount, long outOfStockCount)
        {
            PurchaseValueCents = purchaseValueCents;
            SaleValueCents = saleValueCents;
            ReferenceCount = referenceCount;
            UnitCount = unitCount;
            OutOfStockCount = outOfStockCount;
        }

        /// <summary>
        /// Sum of quantity x current purchase price.
        /// </summary>
        public long PurchaseValueCents { get; }

        /// <summary>
        /// Sum of quantity x current sale price.
        /// </summary>
        public long SaleValueCents { get; }

        public long PotentialMarginCents => SaleValueCents - PurchaseValueCents;

        /// <summary>
        /// Number of products with a quantity above 0.
        /// </summary>
        public long ReferenceCount { get; }

        public long UnitCount { get; }

        /// <summary>
        /// Active (not archived) products with a quantity of 0.
        /// </summary>
        public long OutOfStockCount { get; }
    }
}
