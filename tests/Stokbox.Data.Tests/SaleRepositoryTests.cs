using System;
using System.Data.SQLite;
using System.Linq;
using Dapper;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;
using Stokbox.Core.Validation;
using Stokbox.Data.Migrations;
using Stokbox.Data.Repositories;
using Xunit;

namespace Stokbox.Data.Tests
{
    public class SaleRepositoryTests : IDisposable
    {
        private readonly TempDatabase _database = new TempDatabase();
        private readonly ProductRepository _products;
        private readonly StockMovementRepository _movements;
        private readonly long _categoryId;
        private readonly long _coffeeId;
        private readonly long _waterId;

        // Noon UTC: the same calendar day in local time wherever the tests run between UTC-11 and UTC+11.
        private DateTime _now = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

        public SaleRepositoryTests()
        {
            new MigrationRunner(_database.ConnectionFactory).MigrateToLatest();
            _products = new ProductRepository(_database.ConnectionFactory);
            _movements = new StockMovementRepository(_database.ConnectionFactory);

            _categoryId = new CategoryRepository(_database.ConnectionFactory).Insert("Épicerie");
            _coffeeId = _products.Insert("2000000000015", "Café moulu", _categoryId, 40000, 52050, _now);
            _waterId = _products.Insert("2000000000022", "Eau minérale", _categoryId, 2500, 3500, _now);
            _movements.AddEntry(_coffeeId, 10, _now);
            _movements.AddEntry(_waterId, 24, _now);
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        [Fact]
        public void A_validated_sale_is_stored_with_its_lines_and_takes_the_products_out_of_the_stock()
        {
            var sale = Sell(60000, _coffeeId, _waterId, _waterId);

            Assert.Equal("V-" + _now.ToLocalTime().ToString("yyyyMMdd") + "-0001", sale.Number);
            Assert.Equal(59050, sale.TotalCents);
            Assert.Equal(950, sale.ChangeCents);

            using (var connection = _database.Open())
            {
                Assert.Equal(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sales"));
                Assert.Equal(sale.Number, connection.ExecuteScalar<string>("SELECT number FROM sales WHERE id = @Id", new { sale.Id }));
                Assert.Equal(59050, connection.ExecuteScalar<long>("SELECT total_cents FROM sales"));
                Assert.Equal(60000, connection.ExecuteScalar<long>("SELECT received_cents FROM sales"));
                Assert.Equal(950, connection.ExecuteScalar<long>("SELECT change_cents FROM sales"));
                Assert.Equal("VALIDEE", connection.ExecuteScalar<string>("SELECT status FROM sales"));
                Assert.Equal("2026-10-03T12:00:00.000Z", connection.ExecuteScalar<string>("SELECT created_at FROM sales"));

                Assert.Equal(2, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sale_lines WHERE sale_id = @Id", new { sale.Id }));
                Assert.Equal(2, LineValue(connection, _waterId, "quantity"));
                Assert.Equal(7000, LineValue(connection, _waterId, "line_total_cents"));

                // One VENTE movement per line, negative, linked to the sale.
                Assert.Equal(
                    new long[] { -1, -2 },
                    connection.Query<long>(
                        "SELECT quantity FROM stock_movements WHERE type = 'VENTE' AND sale_id = @Id ORDER BY product_id",
                        new { sale.Id }));
            }

            Assert.Equal(9, _products.GetById(_coffeeId).StockQuantity);
            Assert.Equal(22, _products.GetById(_waterId).StockQuantity);
        }

        // RG-04.

        [Fact]
        public void Sale_lines_keep_the_prices_of_the_sale_when_the_product_prices_change_afterwards()
        {
            Sell(60000, _coffeeId);

            _products.Update(_coffeeId, "Café moulu", _categoryId, 45000, 60000);

            using (var connection = _database.Open())
            {
                Assert.Equal(52050, LineValue(connection, _coffeeId, "unit_price_cents"));
                Assert.Equal(40000, LineValue(connection, _coffeeId, "unit_purchase_price_cents"));
                Assert.Equal(52050, LineValue(connection, _coffeeId, "line_total_cents"));
            }

            Assert.Equal(60000, _products.GetById(_coffeeId).SalePriceCents);
        }

        // RG-07.

        [Fact]
        public void An_error_after_the_lines_are_inserted_cancels_the_whole_sale_and_leaves_no_movement()
        {
            // Simulated failure: the database refuses the second VENTE movement, once the sale,
            // its two lines and the first movement are already inserted.
            using (var connection = _database.Open())
            {
                connection.Execute(
                    "CREATE TRIGGER simulated_failure BEFORE INSERT ON stock_movements " +
                    "WHEN NEW.type = 'VENTE' AND (SELECT COUNT(*) FROM stock_movements WHERE type = 'VENTE') >= 1 " +
                    "BEGIN SELECT RAISE(ABORT, 'panne simulée'); END;");
            }

            var service = CreateService();
            var cart = new Cart();
            service.AddProduct(cart, _coffeeId);
            service.AddProduct(cart, _waterId);

            var error = Assert.Throws<SQLiteException>(() => service.Validate(cart, 60000));

            Assert.Contains("panne simulée", error.Message);
            using (var connection = _database.Open())
            {
                Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sales"));
                Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sale_lines"));
                Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM stock_movements WHERE type = 'VENTE'"));
                Assert.Equal(2, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM stock_movements"));
            }

            Assert.Equal(10, _products.GetById(_coffeeId).StockQuantity);
            Assert.Equal(24, _products.GetById(_waterId).StockQuantity);

            // The cart is kept, so the sale can be tried again.
            Assert.Equal(2, cart.Lines.Count);
        }

        [Fact]
        public void The_sale_goes_through_once_the_failure_is_gone()
        {
            using (var connection = _database.Open())
            {
                connection.Execute(
                    "CREATE TRIGGER simulated_failure BEFORE INSERT ON stock_movements WHEN NEW.type = 'VENTE' " +
                    "BEGIN SELECT RAISE(ABORT, 'panne simulée'); END;");
            }

            var service = CreateService();
            var cart = new Cart();
            service.AddProduct(cart, _coffeeId);
            Assert.Throws<SQLiteException>(() => service.Validate(cart, 60000));

            using (var connection = _database.Open())
            {
                connection.Execute("DROP TRIGGER simulated_failure");
            }

            var sale = service.Validate(cart, 60000);

            // The failed attempt did not consume a number.
            Assert.EndsWith("-0001", sale.Number);
            Assert.Equal(9, _products.GetById(_coffeeId).StockQuantity);
        }

        // RG-10.

        [Fact]
        public void The_stock_is_checked_again_inside_the_transaction()
        {
            var service = CreateService();
            var cart = new Cart();
            service.AddProduct(cart, _waterId);
            service.AddProduct(cart, _coffeeId);
            service.SetQuantity(cart, _coffeeId, 10);

            // The stock drops after the cart was filled: 10 asked, 4 left.
            AddMovement(_coffeeId, "VENTE", -6);

            var error = Assert.Throws<InsufficientStockException>(() => service.Validate(cart, 1000000));

            Assert.Equal("Stock insuffisant pour « Café moulu » : 4 disponible(s)", error.Message);
            using (var connection = _database.Open())
            {
                Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sales"));
                Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sale_lines"));
            }

            Assert.Equal(4, _products.GetById(_coffeeId).StockQuantity);
            Assert.Equal(24, _products.GetById(_waterId).StockQuantity);
        }

        [Fact]
        public void The_whole_stock_of_a_product_can_be_sold_but_not_one_more()
        {
            var service = CreateService();
            var cart = new Cart();
            service.AddProduct(cart, _coffeeId);
            service.SetQuantity(cart, _coffeeId, 10);

            service.Validate(cart, 1000000);

            Assert.Equal(0, _products.GetById(_coffeeId).StockQuantity);
            Assert.Throws<InsufficientStockException>(() => service.AddProduct(cart, _coffeeId));
        }

        [Fact]
        public void A_product_archived_after_the_cart_was_filled_is_refused_inside_the_transaction()
        {
            var service = CreateService();
            var cart = new Cart();
            service.AddProduct(cart, _coffeeId);
            _products.SetArchived(_coffeeId, true);

            Assert.Throws<BusinessRuleException>(() => service.Validate(cart, 60000));

            using (var connection = _database.Open())
            {
                Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sales"));
            }
        }

        // RG-08.

        [Fact]
        public void A_sale_with_less_cash_than_the_total_records_nothing()
        {
            var service = CreateService();
            var cart = new Cart();
            service.AddProduct(cart, _coffeeId);

            Assert.Throws<ValidationException>(() => service.Validate(cart, 52049));

            using (var connection = _database.Open())
            {
                Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sales"));
                Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM stock_movements WHERE type = 'VENTE'"));
            }
        }

        // Numérotation journalière.

        [Fact]
        public void Sale_numbers_follow_each_other_within_a_day_and_start_again_the_next_day()
        {
            var day1 = _now.ToLocalTime().ToString("yyyyMMdd");
            var first = Sell(60000, _waterId);
            var second = Sell(60000, _waterId);
            var third = Sell(60000, _coffeeId);

            _now = _now.AddDays(1);
            var day2 = _now.ToLocalTime().ToString("yyyyMMdd");
            var nextDayFirst = Sell(60000, _waterId);
            var nextDaySecond = Sell(60000, _waterId);

            Assert.Equal("V-" + day1 + "-0001", first.Number);
            Assert.Equal("V-" + day1 + "-0002", second.Number);
            Assert.Equal("V-" + day1 + "-0003", third.Number);
            Assert.Equal("V-" + day2 + "-0001", nextDayFirst.Number);
            Assert.Equal("V-" + day2 + "-0002", nextDaySecond.Number);
            Assert.NotEqual(day1, day2);
        }

        [Fact]
        public void The_number_is_the_local_day_then_a_sequence_on_four_digits()
        {
            var sale = Sell(60000, _waterId);

            Assert.Matches("^V-[0-9]{8}-[0-9]{4}$", sale.Number);
            Assert.Equal("V-" + _now.ToLocalTime().ToString("yyyyMMdd") + "-0001", sale.Number);
        }

        [Fact]
        public void The_sequence_goes_past_9999_without_repeating_a_number()
        {
            var day = _now.ToLocalTime().ToString("yyyyMMdd");
            using (var connection = _database.Open())
            {
                connection.Execute(
                    "INSERT INTO sales (number, created_at, total_cents, received_cents, change_cents, status) " +
                    "VALUES (@Number, @CreatedAt, 0, 0, 0, 'VALIDEE')",
                    new[]
                    {
                        new { Number = "V-" + day + "-9999", CreatedAt = "2026-10-03T08:00:00.000Z" },
                        new { Number = "V-" + day + "-10000", CreatedAt = "2026-10-03T08:00:01.000Z" }
                    });
            }

            Assert.Equal("V-" + day + "-10001", Sell(60000, _waterId).Number);
        }

        private SaleService CreateService()
        {
            return new SaleService(_products, new SaleRepository(_database.ConnectionFactory), () => _now);
        }

        private Sale Sell(long receivedCents, params long[] scannedProductIds)
        {
            var service = CreateService();
            var cart = new Cart();
            foreach (var productId in scannedProductIds)
            {
                service.AddProduct(cart, productId);
            }

            return service.Validate(cart, receivedCents);
        }

        private static long LineValue(SQLiteConnection connection, long productId, string column)
        {
            // The column name comes from the test itself, never from user input.
            return connection.ExecuteScalar<long>(
                "SELECT " + column + " FROM sale_lines WHERE product_id = @ProductId",
                new { ProductId = productId });
        }

        private void AddMovement(long productId, string type, int quantity)
        {
            using (var connection = _database.Open())
            {
                connection.Execute(
                    "INSERT INTO stock_movements (product_id, type, quantity, created_at) " +
                    "VALUES (@ProductId, @Type, @Quantity, @CreatedAt)",
                    new { ProductId = productId, Type = type, Quantity = quantity, CreatedAt = "2026-10-03T09:00:00.000Z" });
            }
        }
    }
}
