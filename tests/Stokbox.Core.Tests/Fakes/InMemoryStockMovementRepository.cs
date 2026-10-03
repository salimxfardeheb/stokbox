using System;
using System.Collections.Generic;
using System.Linq;
using Stokbox.Core.Repositories;

namespace Stokbox.Core.Tests.Fakes
{
    /// <summary>
    /// Keeps the entries and reports them into the stock quantity of the products, as the database sum does.
    /// </summary>
    public sealed class InMemoryStockMovementRepository : IStockMovementRepository
    {
        private readonly InMemoryProductRepository _products;
        private readonly List<KeyValuePair<long, int>> _entries = new List<KeyValuePair<long, int>>();

        public InMemoryStockMovementRepository(InMemoryProductRepository products)
        {
            _products = products;
        }

        public int Count => _entries.Count;

        public DateTime LastCreatedAtUtc { get; private set; }

        public long AddEntry(long productId, int quantity, DateTime createdAtUtc)
        {
            _entries.Add(new KeyValuePair<long, int>(productId, quantity));
            LastCreatedAtUtc = createdAtUtc;
            _products.SetStockQuantity(productId, _entries.Where(e => e.Key == productId).Sum(e => (long)e.Value));
            return _entries.Count;
        }
    }
}
