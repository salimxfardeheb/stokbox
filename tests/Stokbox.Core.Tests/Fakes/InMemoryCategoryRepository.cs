using System.Collections.Generic;
using System.Linq;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;

namespace Stokbox.Core.Tests.Fakes
{
    public sealed class InMemoryCategoryRepository : ICategoryRepository
    {
        private readonly List<Category> _categories = new List<Category>();
        private readonly Dictionary<long, int> _productCounts = new Dictionary<long, int>();
        private long _lastId;

        public IReadOnlyList<Category> GetAll()
        {
            return _categories.OrderBy(c => c.Name).ToList();
        }

        public Category GetById(long id)
        {
            return _categories.FirstOrDefault(c => c.Id == id);
        }

        public long Insert(string name)
        {
            _categories.Add(new Category { Id = ++_lastId, Name = name });
            return _lastId;
        }

        public bool Rename(long id, string name)
        {
            var category = GetById(id);
            if (category == null)
            {
                return false;
            }

            category.Name = name;
            return true;
        }

        public int CountProducts(long id)
        {
            return _productCounts.TryGetValue(id, out var count) ? count : 0;
        }

        public bool Delete(long id)
        {
            return _categories.RemoveAll(c => c.Id == id) > 0;
        }

        public void SetProductCount(long id, int count)
        {
            _productCounts[id] = count;
        }
    }
}
