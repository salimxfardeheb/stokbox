using System;
using System.Globalization;
using Dapper;
using Stokbox.Core.Services;
using Stokbox.Core.Validation;
using Stokbox.Data.Migrations;
using Stokbox.Data.Repositories;
using Xunit;

namespace Stokbox.Data.Tests
{
    public class StockEntryTests : IDisposable
    {
        private readonly TempDatabase _database = new TempDatabase();
        private readonly ProductRepository _products;
        private readonly StockService _service;
        private readonly long _coffeeId;
        private readonly long _waterId;

        public StockEntryTests()
        {
            new MigrationRunner(_database.ConnectionFactory).MigrateToLatest();
            _products = new ProductRepository(_database.ConnectionFactory);
            _service = new StockService(_products, new StockMovementRepository(_database.ConnectionFactory));

            var categoryId = new CategoryRepository(_database.ConnectionFactory).Insert("Épicerie");
            _coffeeId = _products.Insert("2000000000015", "Café moulu", categoryId, 40000, 52050, DateTime.UtcNow);
            _waterId = _products.Insert("2000000000022", "Eau minérale", categoryId, 2500, 3500, DateTime.UtcNow);
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        [Fact]
        public void Stock_is_the_sum_of_the_movements_after_several_entries()
        {
            _service.AddEntry(_coffeeId, 10);
            _service.AddEntry(_coffeeId, 5);
            _service.AddEntry(_waterId, 24);
            var last = _service.AddEntry(_coffeeId, 1);

            Assert.Equal(16, last.StockQuantityAfter);
            Assert.Equal(16, _products.GetById(_coffeeId).StockQuantity);
            Assert.Equal(24, _products.GetById(_waterId).StockQuantity);
        }

        [Fact]
        public void An_entry_is_stored_as_a_dated_ENTREE_movement()
        {
            var entry = _service.AddEntry(_coffeeId, 12);

            using (var connection = _database.Open())
            {
                Assert.Equal(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM stock_movements"));
                Assert.Equal(_coffeeId, connection.ExecuteScalar<long>("SELECT product_id FROM stock_movements"));
                Assert.Equal("ENTREE", connection.ExecuteScalar<string>("SELECT type FROM stock_movements"));
                Assert.Equal(12, connection.ExecuteScalar<int>("SELECT quantity FROM stock_movements"));
                Assert.Equal(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM stock_movements WHERE sale_id IS NULL"));
                Assert.Equal(
                    entry.CreatedAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                    connection.ExecuteScalar<string>("SELECT created_at FROM stock_movements"));
            }
        }

        [Fact]
        public void An_entry_on_an_archived_product_is_refused_and_leaves_the_stock_unchanged()
        {
            _service.AddEntry(_coffeeId, 10);
            _products.SetArchived(_coffeeId, true);

            Assert.Throws<BusinessRuleException>(() => _service.AddEntry(_coffeeId, 5));
            Assert.Equal(10, _products.GetById(_coffeeId).StockQuantity);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void A_quantity_of_zero_or_less_is_refused_and_records_nothing(int quantity)
        {
            Assert.Throws<ValidationException>(() => _service.AddEntry(_coffeeId, quantity));

            using (var connection = _database.Open())
            {
                Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM stock_movements"));
            }
        }

        [Fact]
        public void The_repository_itself_only_records_positive_entries()
        {
            var movements = new StockMovementRepository(_database.ConnectionFactory);

            Assert.Throws<ArgumentOutOfRangeException>(() => movements.AddEntry(_coffeeId, 0, DateTime.UtcNow));
            Assert.Throws<ArgumentOutOfRangeException>(() => movements.AddEntry(_coffeeId, -1, DateTime.UtcNow));
        }

        [Fact]
        public void A_product_is_found_by_its_exact_barcode_even_when_archived()
        {
            _products.SetArchived(_waterId, true);

            Assert.Equal("Café moulu", _products.GetByBarcode("2000000000015").Name);
            Assert.True(_products.GetByBarcode("2000000000022").IsArchived);
            Assert.Null(_products.GetByBarcode("200000000001"));
            Assert.Null(_products.GetByBarcode("2000000000039"));
        }
    }
}
