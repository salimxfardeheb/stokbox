using System;
using System.Collections.Generic;
using System.Linq;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;

namespace Stokbox.Core.Tests.Fakes
{
    public sealed class InMemoryProductRepository : IProductRepository
    {
        private readonly List<Product> _products = new List<Product>();
        private long _lastId;

        public ProductSearchCriteria LastCriteria { get; private set; }

        public int Count => _products.Count;

        public Product GetById(long id)
        {
            return _products.FirstOrDefault(p => p.Id == id);
        }

        public IReadOnlyList<Product> Search(ProductSearchCriteria criteria)
        {
            LastCriteria = criteria;
            return _products.Where(p => criteria.IncludeArchived || !p.IsArchived).ToList();
        }

        public long Insert(string barcode, string name, long categoryId, long purchasePriceCents, long salePriceCents, DateTime createdAtUtc)
        {
            _products.Add(new Product
            {
                Id = ++_lastId,
                Barcode = barcode,
                Name = name,
                CategoryId = categoryId,
                PurchasePriceCents = purchasePriceCents,
                SalePriceCents = salePriceCents
            });
            return _lastId;
        }

        public bool Update(long id, string name, long categoryId, long purchasePriceCents, long salePriceCents)
        {
            var product = GetById(id);
            if (product == null)
            {
                return false;
            }

            product.Name = name;
            product.CategoryId = categoryId;
            product.PurchasePriceCents = purchasePriceCents;
            product.SalePriceCents = salePriceCents;
            return true;
        }

        public bool SetArchived(long id, bool isArchived)
        {
            var product = GetById(id);
            if (product == null)
            {
                return false;
            }

            product.IsArchived = isArchived;
            return true;
        }
    }
}
