namespace Stokbox.Core.Dashboard
{
    public sealed class CategorySales
    {
        public CategorySales(string categoryName, long netRevenueCents)
        {
            CategoryName = categoryName;
            NetRevenueCents = netRevenueCents;
        }

        /// <summary>
        /// Current category of the products sold; null for products without a category.
        /// </summary>
        public string CategoryName { get; }

        public long NetRevenueCents { get; }
    }
}
