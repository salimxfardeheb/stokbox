using System;
using System.Collections.Generic;
using Stokbox.Core.Entities;

namespace Stokbox.Core.Repositories
{
    public interface IProductRepository
    {
        Product GetById(long id);

        /// <summary>
        /// Products matching the criteria, sorted by name, with their stock quantity.
        /// </summary>
        IReadOnlyList<Product> Search(ProductSearchCriteria criteria);

        long Insert(string barcode, string name, long categoryId, long purchasePriceCents, long salePriceCents, DateTime createdAtUtc);

        /// <summary>
        /// Updates everything but the barcode, which is never modified (RG-02).
        /// </summary>
        /// <returns>False when the product does not exist.</returns>
        bool Update(long id, string name, long categoryId, long purchasePriceCents, long salePriceCents);

        /// <returns>False when the product does not exist.</returns>
        bool SetArchived(long id, bool isArchived);
    }
}
