namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// One entry of the category filter; a null id means every category.
    /// </summary>
    public sealed class CategoryFilterOption
    {
        public CategoryFilterOption(long? id, string name)
        {
            Id = id;
            Name = name;
        }

        public long? Id { get; }

        public string Name { get; }
    }
}
