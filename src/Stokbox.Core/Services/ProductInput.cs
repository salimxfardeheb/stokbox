namespace Stokbox.Core.Services
{
    /// <summary>
    /// What the user enters to create or modify a product. Prices are in DA.
    /// </summary>
    public sealed class ProductInput
    {
        public string Name { get; set; }

        public long? CategoryId { get; set; }

        public decimal? PurchasePrice { get; set; }

        public decimal? SalePrice { get; set; }
    }
}
