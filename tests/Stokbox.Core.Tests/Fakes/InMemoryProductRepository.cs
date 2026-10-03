using System;
using System.Collections.Generic;
using System.Linq;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;

namespace Stokbox.Core.Tests.Fakes
{
    /// <summary>
    /// Like the database, every read returns a snapshot: later changes do not alter it.
    /// </summary>
    public sealed class InMemoryProductRepository : IProductRepository
    {
        private readonly List<Product> _products = new List<Product>();
        private long _lastId;

        public ProductSearchCriteria LastCriteria { get; private set; }

        public int Count => _products.Count;

        public Product GetById(long id)
        {
            return Copy(Find(id));
        }

        public Product GetByBarcode(string barcode)
        {
            return Copy(_products.FirstOrDefault(p => p.Barcode == barcode));
        }

        public IReadOnlyList<Product> Search(ProductSearchCriteria criteria)
        {
            LastCriteria = criteria;
            return _products.Where(p => criteria.IncludeArchived || !p.IsArchived).Select(Copy).ToList();
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
            var product = Find(id);
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
            var product = Find(id);
            if (product == null)
            {
                return false;
            }

            product.IsArchived = isArchived;
            return true;
        }

        public void SetStockQuantity(long id, long quantity)
        {
            Find(id).StockQuantity = quantity;
        }

        private Product Find(long id)
        {
            return _products.FirstOrDefault(p => p.Id == id);
        }

        private static Product Copy(Product product)
        {
            if (product == null)
            {
                return null;
            }

            return new Product
            {
                Id = product.Id,
                Barcode = product.Barcode,
                Name = product.Name,
                CategoryId = product.CategoryId,
                CategoryName = product.CategoryName,
                PurchasePriceCents = product.PurchasePriceCents,
                SalePriceCents = product.SalePriceCents,
                IsArchived = product.IsArchived,
                StockQuantity = product.StockQuantity
            };
        }
    }
}
