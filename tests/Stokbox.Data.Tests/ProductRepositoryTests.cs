using System;
using System.Linq;
using Dapper;
using Stokbox.Core.Repositories;
using Stokbox.Core.Services;
using Stokbox.Data.Migrations;
using Stokbox.Data.Repositories;
using Xunit;

namespace Stokbox.Data.Tests
{
    public class ProductRepositoryTests : IDisposable
    {
        private readonly TempDatabase _database = new TempDatabase();
        private readonly CategoryRepository _categories;
        private readonly ProductRepository _products;
        private readonly long _drinksId;
        private readonly long _groceryId;

        public ProductRepositoryTests()
        {
            new MigrationRunner(_database.ConnectionFactory).MigrateToLatest();
            _categories = new CategoryRepository(_database.ConnectionFactory);
            _products = new ProductRepository(_database.ConnectionFactory);
            _drinksId = _categories.Insert("Boissons");
            _groceryId = _categories.Insert("Épicerie");
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        [Fact]
        public void An_inserted_product_is_read_back_with_its_category()
        {
            var id = Insert("2000000000015", "Café moulu", _groceryId, 40000, 52050);

            var product = _products.GetById(id);

            Assert.Equal("2000000000015", product.Barcode);
            Assert.Equal("Café moulu", product.Name);
            Assert.Equal(_groceryId, product.CategoryId);
            Assert.Equal("Épicerie", product.CategoryName);
            Assert.Equal(40000, product.PurchasePriceCents);
            Assert.Equal(52050, product.SalePriceCents);
            Assert.False(product.IsArchived);
            Assert.Equal(0, product.StockQuantity);
        }

        [Fact]
        public void Creation_date_is_stored_in_UTC_ISO_8601()
        {
            var id = _products.Insert(
                "2000000000015", "Café moulu", _groceryId, 40000, 52050,
                new DateTime(2026, 3, 9, 14, 5, 7, 120, DateTimeKind.Utc));

            using (var connection = _database.Open())
            {
                Assert.Equal(
                    "2026-03-09T14:05:07.120Z",
                    connection.ExecuteScalar<string>("SELECT created_at FROM products WHERE id = @Id", new { Id = id }));
            }
        }

        [Fact]
        public void A_missing_product_is_reported()
        {
            Assert.Null(_products.GetById(99));
            Assert.False(_products.Update(99, "Café", _groceryId, 1, 2));
            Assert.False(_products.SetArchived(99, true));
        }

        [Fact]
        public void Update_changes_everything_but_the_barcode()
        {
            var id = Insert("2000000000015", "Café moulu", _groceryId, 40000, 52050);

            Assert.True(_products.Update(id, "Jus d'orange", _drinksId, 8000, 12000));

            var product = _products.GetById(id);
            Assert.Equal("2000000000015", product.Barcode);
            Assert.Equal("Jus d'orange", product.Name);
            Assert.Equal("Boissons", product.CategoryName);
            Assert.Equal(8000, product.PurchasePriceCents);
            Assert.Equal(12000, product.SalePriceCents);
        }

        [Fact]
        public void Search_without_criteria_returns_the_active_products_sorted_by_name()
        {
            Insert("2000000000039", "thé vert", _groceryId);
            Insert("2000000000015", "Café moulu", _groceryId);
            Insert("2000000000022", "Eau minérale", _drinksId);

            Assert.Equal(new[] { "Café moulu", "Eau minérale", "thé vert" }, SearchNames(new ProductSearchCriteria()));
        }

        [Fact]
        public void Search_finds_a_product_by_its_exact_barcode()
        {
            Insert("2000000000015", "Café moulu", _groceryId);
            Insert("2000000000022", "Eau minérale", _drinksId);

            Assert.Equal(new[] { "Eau minérale" }, SearchNames(new ProductSearchCriteria { Text = "2000000000022" }));
            Assert.Empty(SearchNames(new ProductSearchCriteria { Text = "200000000002" }));
        }

        [Theory]
        [InlineData("café")]
        [InlineData("CAFE")]
        [InlineData("cafe")]
        [InlineData("FÉ MOU")]
        [InlineData("  moulu ")]
        public void Search_by_name_ignores_case_and_accents(string text)
        {
            Insert("2000000000015", "Café moulu", _groceryId);
            Insert("2000000000022", "Eau minérale", _drinksId);

            Assert.Equal(new[] { "Café moulu" }, SearchNames(new ProductSearchCriteria { Text = text }));
        }

        [Fact]
        public void Search_text_is_matched_literally()
        {
            Insert("2000000000015", "Remise 100%", _groceryId);
            Insert("2000000000022", "Remise 50", _groceryId);

            Assert.Equal(new[] { "Remise 100%" }, SearchNames(new ProductSearchCriteria { Text = "%" }));
            Assert.Empty(SearchNames(new ProductSearchCriteria { Text = "_" }));
        }

        [Fact]
        public void Search_filters_by_category()
        {
            Insert("2000000000015", "Café moulu", _groceryId);
            Insert("2000000000022", "Eau minérale", _drinksId);

            Assert.Equal(new[] { "Eau minérale" }, SearchNames(new ProductSearchCriteria { CategoryId = _drinksId }));
            Assert.Empty(SearchNames(new ProductSearchCriteria { CategoryId = _drinksId, Text = "café" }));
        }

        [Fact]
        public void Search_excludes_archived_products_unless_asked()
        {
            Insert("2000000000015", "Café moulu", _groceryId);
            var archivedId = Insert("2000000000022", "Eau minérale", _drinksId);
            _products.SetArchived(archivedId, true);

            Assert.Equal(new[] { "Café moulu" }, SearchNames(new ProductSearchCriteria()));
            Assert.Equal(
                new[] { "Café moulu", "Eau minérale" },
                SearchNames(new ProductSearchCriteria { IncludeArchived = true }));
            Assert.True(_products.GetById(archivedId).IsArchived);

            _products.SetArchived(archivedId, false);
            Assert.Equal(new[] { "Café moulu", "Eau minérale" }, SearchNames(new ProductSearchCriteria()));
        }

        [Fact]
        public void Stock_quantity_is_the_sum_of_the_movements_of_the_product()
        {
            var coffeeId = Insert("2000000000015", "Café moulu", _groceryId);
            var waterId = Insert("2000000000022", "Eau minérale", _drinksId);
            AddMovement(coffeeId, "ENTREE", 10);
            AddMovement(coffeeId, "VENTE", -3);
            AddMovement(coffeeId, "RETOUR", 1);
            AddMovement(waterId, "ENTREE", 24);

            Assert.Equal(8, _products.GetById(coffeeId).StockQuantity);
            Assert.Equal(
                new long[] { 8, 24 },
                _products.Search(new ProductSearchCriteria()).Select(p => p.StockQuantity));
        }

        [Fact]
        public void Products_created_through_the_service_get_distinct_valid_barcodes_from_the_database_sequence()
        {
            var service = new ProductService(
                _products,
                _categories,
                new BarcodeGenerator(new SqliteBarcodeSequence(_database.ConnectionFactory)));
            var input = new ProductInput { Name = "Café moulu", CategoryId = _groceryId, PurchasePrice = 400m, SalePrice = 520.50m };

            var first = service.Create(input);
            var second = service.Create(input);

            Assert.Equal("2000000000015", first.Barcode);
            Assert.Equal("2000000000022", second.Barcode);
            Assert.Equal(52050, first.SalePriceCents);
            Assert.Equal("Épicerie", first.CategoryName);
        }

        private long Insert(string barcode, string name, long categoryId, long purchasePriceCents = 1000, long salePriceCents = 1500)
        {
            return _products.Insert(barcode, name, categoryId, purchasePriceCents, salePriceCents, DateTime.UtcNow);
        }

        private string[] SearchNames(ProductSearchCriteria criteria)
        {
            return _products.Search(criteria).Select(p => p.Name).ToArray();
        }

        private void AddMovement(long productId, string type, int quantity)
        {
            using (var connection = _database.Open())
            {
                connection.Execute(
                    "INSERT INTO stock_movements (product_id, type, quantity, created_at) " +
                    "VALUES (@ProductId, @Type, @Quantity, @CreatedAt)",
                    new { ProductId = productId, Type = type, Quantity = quantity, CreatedAt = "2026-01-01T00:00:00Z" });
            }
        }
    }
}
