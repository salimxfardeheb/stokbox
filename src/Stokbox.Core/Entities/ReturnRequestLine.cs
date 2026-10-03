namespace Stokbox.Core.Entities
{
    /// <summary>
    /// A quantity of one sale line that the customer brings back.
    /// </summary>
    public sealed class ReturnRequestLine
    {
        public ReturnRequestLine(long saleLineId, int quantity)
        {
            SaleLineId = saleLineId;
            Quantity = quantity;
        }

        public long SaleLineId { get; }

        public int Quantity { get; }
    }
}
