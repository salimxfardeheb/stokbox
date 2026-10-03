namespace Stokbox.Core.Repositories
{
    public sealed class ProductSearchCriteria
    {
        /// <summary>
        /// Exact barcode, or part of the name (case and accents ignored). Empty: no text filter.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Null: every category.
        /// </summary>
        public long? CategoryId { get; set; }

        public bool IncludeArchived { get; set; }

        /// <summary>
        /// Only the active (not archived) products with a stock quantity of 0.
        /// </summary>
        public bool OutOfStockOnly { get; set; }
    }
}
