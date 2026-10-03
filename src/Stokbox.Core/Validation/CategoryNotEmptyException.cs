namespace Stokbox.Core.Validation
{
    public sealed class CategoryNotEmptyException : BusinessRuleException
    {
        public CategoryNotEmptyException()
            : base("Cette catégorie contient des produits (archivés compris) : elle ne peut pas être supprimée.")
        {
        }
    }
}
