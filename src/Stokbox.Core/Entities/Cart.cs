using System.Collections.Generic;
using System.Linq;

namespace Stokbox.Core.Entities
{
    /// <summary>
    /// The sale being prepared, in memory only. One line per product.
    /// It is modified through SaleService, which enforces the stock rules.
    /// </summary>
    public sealed class Cart
    {
        private readonly List<CartLine> _lines = new List<CartLine>();

        public IReadOnlyList<CartLine> Lines => _lines;

        public bool IsEmpty => _lines.Count == 0;

        public long TotalCents => _lines.Sum(line => line.LineTotalCents);

        /// <summary>
        /// Number of articles: the sum of the quantities.
        /// </summary>
        public int ArticleCount => _lines.Sum(line => line.Quantity);

        public CartLine Find(long productId)
        {
            return _lines.FirstOrDefault(line => line.ProductId == productId);
        }

        public void Clear()
        {
            _lines.Clear();
        }

        internal void Add(CartLine line)
        {
            _lines.Add(line);
        }

        internal void Remove(CartLine line)
        {
            _lines.Remove(line);
        }
    }
}
