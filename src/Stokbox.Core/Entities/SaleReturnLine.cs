namespace Stokbox.Core.Entities
{
    public sealed class SaleReturnLine
    {
        public long SaleLineId { get; set; }

        public string ProductName { get; set; }

        public int Quantity { get; set; }

        /// <summary>
        /// Price frozen on the sale line (RG-04), not the current price of the product.
        /// </summary>
        public long UnitPriceCents { get; set; }

        public long AmountCents => UnitPriceCents * Quantity;
    }
}
