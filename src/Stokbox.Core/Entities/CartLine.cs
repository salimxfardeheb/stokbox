namespace Stokbox.Core.Entities
{
    public sealed class CartLine
    {
        internal CartLine(Product product, int quantity)
        {
            ProductId = product.Id;
            Barcode = product.Barcode;
            Quantity = quantity;
            CopyDetailsFrom(product);
        }

        public long ProductId { get; }

        public string Barcode { get; }

        public string Name { get; private set; }

        public long UnitPriceCents { get; private set; }

        public long UnitPurchasePriceCents { get; private set; }

        public int Quantity { get; internal set; }

        public long LineTotalCents => UnitPriceCents * Quantity;

        // Name and prices follow the product until the sale is validated, which freezes them (RG-04).
        internal void CopyDetailsFrom(Product product)
        {
            Name = product.Name;
            UnitPriceCents = product.SalePriceCents;
            UnitPurchasePriceCents = product.PurchasePriceCents;
        }
    }
}
