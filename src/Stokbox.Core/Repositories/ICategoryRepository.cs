using System.Collections.Generic;
using Stokbox.Core.Entities;

namespace Stokbox.Core.Repositories
{
    public interface ICategoryRepository
    {
        /// <summary>
        /// All categories, sorted by name.
        /// </summary>
        IReadOnlyList<Category> GetAll();

        Category GetById(long id);

        long Insert(string name);

        /// <returns>False when the category does not exist.</returns>
        bool Rename(long id, string name);

        /// <summary>
        /// Number of products of the category, archived ones included.
        /// </summary>
        int CountProducts(long id);

        /// <returns>False when the category does not exist.</returns>
        /// <exception cref="Validation.CategoryNotEmptyException">The category still has products.</exception>
        bool Delete(long id);
    }
}
