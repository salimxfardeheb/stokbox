using System;
using System.Linq;
using Dapper;
using Stokbox.Core.Services;
using Stokbox.Core.Validation;
using Stokbox.Data.Migrations;
using Stokbox.Data.Repositories;
using Xunit;

namespace Stokbox.Data.Tests
{
    public class CategoryRepositoryTests : IDisposable
    {
        private readonly TempDatabase _database = new TempDatabase();
        private readonly CategoryRepository _categories;
        private readonly ProductRepository _products;

        public CategoryRepositoryTests()
        {
            new MigrationRunner(_database.ConnectionFactory).MigrateToLatest();
            _categories = new CategoryRepository(_database.ConnectionFactory);
            _products = new ProductRepository(_database.ConnectionFactory);
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        [Fact]
        public void Categories_are_listed_by_name_ignoring_case_and_accents()
        {
            _categories.Insert("Hygiène");
            _categories.Insert("épicerie");
            _categories.Insert("Boissons");

            Assert.Equal(new[] { "Boissons", "épicerie", "Hygiène" }, _categories.GetAll().Select(c => c.Name));
        }

        [Fact]
        public void A_category_is_inserted_read_and_renamed()
        {
            var id = _categories.Insert("Boissons");

            Assert.Equal("Boissons", _categories.GetById(id).Name);
            Assert.True(_categories.Rename(id, "Boissons fraîches"));
            Assert.Equal("Boissons fraîches", _categories.GetById(id).Name);
        }

        [Fact]
        public void A_missing_category_is_reported()
        {
            Assert.Null(_categories.GetById(99));
            Assert.False(_categories.Rename(99, "Boissons"));
            Assert.False(_categories.Delete(99));
        }

        [Fact]
        public void An_empty_category_is_deleted()
        {
            var id = _categories.Insert("Boissons");

            Assert.True(_categories.Delete(id));
            Assert.Empty(_categories.GetAll());
        }

        [Fact]
        public void Deleting_a_category_that_contains_products_is_refused()
        {
            var id = _categories.Insert("Boissons");
            _products.Insert("2000000000015", "Eau minérale", id, 2500, 3500, DateTime.UtcNow);

            Assert.Equal(1, _categories.CountProducts(id));
            Assert.Throws<CategoryNotEmptyException>(() => _categories.Delete(id));
            Assert.Throws<CategoryNotEmptyException>(() => new CategoryService(_categories).Delete(id));
            Assert.NotNull(_categories.GetById(id));
        }

        [Fact]
        public void Deleting_a_category_that_only_contains_archived_products_is_refused()
        {
            var id = _categories.Insert("Boissons");
            var productId = _products.Insert("2000000000015", "Eau minérale", id, 2500, 3500, DateTime.UtcNow);
            _products.SetArchived(productId, true);

            Assert.Throws<CategoryNotEmptyException>(() => _categories.Delete(id));
            Assert.NotNull(_categories.GetById(id));
        }

        [Fact]
        public void The_database_itself_refuses_to_delete_a_category_that_contains_products()
        {
            var id = _categories.Insert("Boissons");
            _products.Insert("2000000000015", "Eau minérale", id, 2500, 3500, DateTime.UtcNow);

            using (var connection = _database.Open())
            {
                Assert.Throws<System.Data.SQLite.SQLiteException>(
                    () => connection.Execute("DELETE FROM categories WHERE id = @Id", new { Id = id }));
            }
        }
    }
}
