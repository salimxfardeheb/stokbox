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
    public class SaleCancelAndReturnTests : IDisposable
    {
        private readonly TempDatabase _database = new TempDatabase();
        private readonly ProductRepository _products;
        private readonly SaleService _service;
        private readonly long _categoryId;
        private readonly long _coffeeId;
        private readonly long _waterId;

        // Noon UTC: the same calendar day in local time wherever the tests run between UTC-11 and UTC+11.
        private DateTime _now = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

        public SaleCancelAndReturnTests()
        {
            new MigrationRunner(_database.ConnectionFactory).MigrateToLatest();
            _products = new ProductRepository(_database.ConnectionFactory);
            _service = new SaleService(_products, new SaleRepository(_database.ConnectionFactory), () => _now);

            var movements = new StockMovementRepository(_database.ConnectionFactory);
            _categoryId = new CategoryRepository(_database.ConnectionFactory).Insert("Épicerie");
            _coffeeId = _products.Insert("2000000000015", "Café moulu", _categoryId, 40000, 52050, _now);
            _waterId = _products.Insert("2000000000022", "Eau minérale", _categoryId, 2500, 3500, _now);
            movements.AddEntry(_coffeeId, 10, _now);
            movements.AddEntry(_waterId, 24, _now);
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        // Annulation.

        [Fact]
        public void Cancelling_a_sale_marks_it_cancelled_and_puts_every_line_back_in_stock()
        {
            var sale = Sell(coffee: 5, water: 2);
            Assert.Equal(5, Stock(_coffeeId));
            Assert.Equal(22, Stock(_waterId));

            _now = _now.AddHours(1);
            _service.CancelSale(sale.Id);

            var cancelled = _service.GetSale(sale.Id);
            Assert.Equal(Sale.StatusCancelled, cancelled.Status);
            Assert.Equal(10, Stock(_coffeeId));
            Assert.Equal(24, Stock(_waterId));

            using (var connection = _database.Open())
            {
                // One positive ANNULATION movement per line, linked to the sale and dated at the cancellation.
                Assert.Equal(
                    new long[] { 5, 2 },
                    connection.Query<long>(
                        "SELECT quantity FROM stock_movements WHERE type = 'ANNULATION' AND sale_id = @Id ORDER BY product_id",
                        new { sale.Id }));
                Assert.Equal(
                    "2026-10-03T13:00:00.000Z",
                    connection.ExecuteScalar<string>("SELECT MIN(created_at) FROM stock_movements WHERE type = 'ANNULATION'"));
            }
        }

        [Fact]
        public void A_cancelled_sale_keeps_everything_but_its_status()
        {
            var sale = Sell(coffee: 5, water: 2);

            _service.CancelSale(sale.Id);

            var cancelled = _service.GetSale(sale.Id);
            Assert.Equal(sale.Number, cancelled.Number);
            Assert.Equal(sale.CreatedAtUtc, cancelled.CreatedAtUtc);
            Assert.Equal(sale.TotalCents, cancelled.TotalCents);
            Assert.Equal(sale.ReceivedCents, cancelled.ReceivedCents);
            Assert.Equal(sale.ChangeCents, cancelled.ChangeCents);
            Assert.Equal(new[] { 5, 2 }, cancelled.Lines.Select(l => l.Quantity));
            Assert.Equal(new long[] { 52050, 3500 }, cancelled.Lines.Select(l => l.UnitPriceCents));

            using (var connection = _database.Open())
            {
                // The VENTE movements stay: the stock is the sum of everything that happened (RG-01).
                Assert.Equal(2, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM stock_movements WHERE type = 'VENTE'"));
            }
        }

        [Fact]
        public void Cancelling_an_already_cancelled_sale_is_refused()
        {
            var sale = Sell(coffee: 5, water: 2);
            _service.CancelSale(sale.Id);

            var error = Assert.Throws<BusinessRuleException>(() => _service.CancelSale(sale.Id));

            Assert.Equal("Cette vente est déjà annulée.", error.Message);
            Assert.Equal(10, Stock(_coffeeId));
            Assert.Equal(2, Count("stock_movements WHERE type = 'ANNULATION'"));
        }

        [Fact]
        public void Cancelling_after_a_return_is_refused()
        {
            var sale = Sell(coffee: 5, water: 2);
            _service.ReturnItems(sale.Id, new[] { new ReturnRequestLine(LineId(sale, _waterId), 1) });

            var error = Assert.Throws<BusinessRuleException>(() => _service.CancelSale(sale.Id));

            Assert.Contains("retour", error.Message);
            Assert.Equal(Sale.StatusValidated, _service.GetSale(sale.Id).Status);
            Assert.Equal(0, Count("stock_movements WHERE type = 'ANNULATION'"));
            Assert.Equal(5, Stock(_coffeeId));
            Assert.Equal(23, Stock(_waterId));
        }

        [Fact]
        public void Cancelling_a_missing_sale_is_refused()
        {
            Assert.Throws<BusinessRuleException>(() => _service.CancelSale(999));
        }

        [Fact]
        public void A_cancellation_that_fails_halfway_leaves_the_sale_validated()
        {
            var sale = Sell(coffee: 5, water: 2);
            using (var connection = _database.Open())
            {
                connection.Execute(
                    "CREATE TRIGGER simulated_failure BEFORE INSERT ON stock_movements " +
                    "WHEN NEW.type = 'ANNULATION' AND (SELECT COUNT(*) FROM stock_movements WHERE type = 'ANNULATION') >= 1 " +
                    "BEGIN SELECT RAISE(ABORT, 'panne simulée'); END;");
            }

            Assert.Throws<SQLiteException>(() => _service.CancelSale(sale.Id));

            Assert.Equal(Sale.StatusValidated, _service.GetSale(sale.Id).Status);
            Assert.Equal(0, Count("stock_movements WHERE type = 'ANNULATION'"));
            Assert.Equal(5, Stock(_coffeeId));
        }

        // Retours (RG-06).

        [Fact]
        public void A_return_is_recorded_with_its_lines_and_puts_the_articles_back_in_stock()
        {
            var sale = Sell(coffee: 5, water: 2);

            _now = _now.AddHours(2);
            var saleReturn = _service.ReturnItems(sale.Id, new[]
            {
                new ReturnRequestLine(LineId(sale, _coffeeId), 2),
                new ReturnRequestLine(LineId(sale, _waterId), 1)
            });

            Assert.Equal(2 * 52050 + 3500, saleReturn.RefundCents);
            Assert.Equal(new[] { "Café moulu", "Eau minérale" }, saleReturn.Lines.Select(l => l.ProductName));
            Assert.Equal(7, Stock(_coffeeId));
            Assert.Equal(23, Stock(_waterId));

            using (var connection = _database.Open())
            {
                Assert.Equal(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM returns WHERE sale_id = @Id", new { sale.Id }));
                Assert.Equal("2026-10-03T14:00:00.000Z", connection.ExecuteScalar<string>("SELECT created_at FROM returns"));
                Assert.Equal(
                    new long[] { 2, 1 },
                    connection.Query<long>("SELECT quantity FROM return_lines WHERE return_id = @Id ORDER BY id", new { saleReturn.Id }));
                Assert.Equal(
                    new long[] { 2, 1 },
                    connection.Query<long>(
                        "SELECT quantity FROM stock_movements WHERE type = 'RETOUR' AND sale_id = @Id ORDER BY product_id",
                        new { sale.Id }));
            }

            // The sale itself is untouched and stays validated.
            var after = _service.GetSale(sale.Id);
            Assert.Equal(Sale.StatusValidated, after.Status);
            Assert.Equal(sale.TotalCents, after.TotalCents);
            Assert.Equal(new[] { 2, 1 }, after.Lines.Select(l => l.ReturnedQuantity));
            Assert.Equal(new[] { 3, 1 }, after.Lines.Select(l => l.ReturnableQuantity));
        }

        [Fact]
        public void The_refund_uses_the_prices_frozen_at_the_sale_not_the_current_ones()
        {
            var sale = Sell(coffee: 5, water: 2);
            _products.Update(_coffeeId, "Café moulu", _categoryId, 45000, 60000);

            var saleReturn = _service.ReturnItems(sale.Id, new[] { new ReturnRequestLine(LineId(sale, _coffeeId), 2) });

            Assert.Equal(52050, saleReturn.Lines.Single().UnitPriceCents);
            Assert.Equal(104100, saleReturn.RefundCents);
            Assert.Equal(104100, _service.GetReturns(sale.Id).Single().RefundCents);
        }

        [Fact]
        public void Stock_is_right_after_a_sale_a_partial_return_and_a_second_return()
        {
            var sale = Sell(coffee: 5, water: 2);
            var coffeeLine = LineId(sale, _coffeeId);
            Assert.Equal(5, Stock(_coffeeId));

            _service.ReturnItems(sale.Id, new[] { new ReturnRequestLine(coffeeLine, 2) });
            Assert.Equal(7, Stock(_coffeeId));
            Assert.Equal(3, _service.GetSale(sale.Id).Lines[0].ReturnableQuantity);

            _service.ReturnItems(sale.Id, new[] { new ReturnRequestLine(coffeeLine, 3) });
            Assert.Equal(10, Stock(_coffeeId));
            Assert.Equal(0, _service.GetSale(sale.Id).Lines[0].ReturnableQuantity);

            // Everything is back: nothing more can be returned on this line.
            Assert.Throws<BusinessRuleException>(
                () => _service.ReturnItems(sale.Id, new[] { new ReturnRequestLine(coffeeLine, 1) }));
            Assert.Equal(10, Stock(_coffeeId));
            Assert.Equal(22, Stock(_waterId));

            var returns = _service.GetReturns(sale.Id);
            Assert.Equal(new[] { 2, 3 }, returns.Select(r => r.Lines.Single().Quantity));
            Assert.Equal(5 * 52050, returns.Sum(r => r.RefundCents));
        }

        [Fact]
        public void A_return_beyond_the_quantity_sold_is_refused()
        {
            var sale = Sell(coffee: 5, water: 2);

            var error = Assert.Throws<BusinessRuleException>(
                () => _service.ReturnItems(sale.Id, new[] { new ReturnRequestLine(LineId(sale, _coffeeId), 6) }));

            Assert.Equal("Retour impossible pour « Café moulu » : 5 retournable(s).", error.Message);
            AssertNoReturnRecorded();
        }

        [Fact]
        public void A_return_beyond_the_quantity_remaining_after_a_first_return_is_refused()
        {
            var sale = Sell(coffee: 5, water: 2);
            var coffeeLine = LineId(sale, _coffeeId);
            _service.ReturnItems(sale.Id, new[] { new ReturnRequestLine(coffeeLine, 4) });

            var error = Assert.Throws<BusinessRuleException>(
                () => _service.ReturnItems(sale.Id, new[] { new ReturnRequestLine(coffeeLine, 2) }));

            Assert.Equal("Retour impossible pour « Café moulu » : 1 retournable(s).", error.Message);
            Assert.Equal(1, Count("returns"));
            Assert.Equal(9, Stock(_coffeeId));
        }

        [Fact]
        public void The_same_line_given_twice_cannot_get_around_the_limit()
        {
            var sale = Sell(coffee: 5, water: 2);
            var coffeeLine = LineId(sale, _coffeeId);

            Assert.Throws<BusinessRuleException>(() => _service.ReturnItems(sale.Id, new[]
            {
                new ReturnRequestLine(coffeeLine, 3),
                new ReturnRequestLine(coffeeLine, 3)
            }));

            AssertNoReturnRecorded();
        }

        [Fact]
        public void One_refused_line_refuses_the_whole_return()
        {
            var sale = Sell(coffee: 5, water: 2);

            Assert.Throws<BusinessRuleException>(() => _service.ReturnItems(sale.Id, new[]
            {
                new ReturnRequestLine(LineId(sale, _coffeeId), 1),
                new ReturnRequestLine(LineId(sale, _waterId), 3)
            }));

            AssertNoReturnRecorded();
            Assert.Equal(5, Stock(_coffeeId));
        }

        [Fact]
        public void A_line_of_another_sale_cannot_be_returned()
        {
            var sale = Sell(coffee: 1, water: 1);
            var other = Sell(coffee: 1, water: 1);

            Assert.Throws<BusinessRuleException>(
                () => _service.ReturnItems(sale.Id, new[] { new ReturnRequestLine(LineId(other, _coffeeId), 1) }));

            AssertNoReturnRecorded();
        }

        [Fact]
        public void A_return_on_a_cancelled_sale_is_refused()
        {
            var sale = Sell(coffee: 5, water: 2);
            _service.CancelSale(sale.Id);

            var error = Assert.Throws<BusinessRuleException>(
                () => _service.ReturnItems(sale.Id, new[] { new ReturnRequestLine(LineId(sale, _coffeeId), 1) }));

            Assert.Contains("annulée", error.Message);
            AssertNoReturnRecorded();
            Assert.Equal(10, Stock(_coffeeId));
        }

        [Fact]
        public void A_return_on_a_missing_sale_is_refused()
        {
            Assert.Throws<BusinessRuleException>(() => _service.ReturnItems(999, new[] { new ReturnRequestLine(1, 1) }));
        }

        [Fact]
        public void A_return_that_fails_halfway_records_nothing()
        {
            var sale = Sell(coffee: 5, water: 2);
            using (var connection = _database.Open())
            {
                connection.Execute(
                    "CREATE TRIGGER simulated_failure BEFORE INSERT ON stock_movements " +
                    "WHEN NEW.type = 'RETOUR' AND (SELECT COUNT(*) FROM stock_movements WHERE type = 'RETOUR') >= 1 " +
                    "BEGIN SELECT RAISE(ABORT, 'panne simulée'); END;");
            }

            Assert.Throws<SQLiteException>(() => _service.ReturnItems(sale.Id, new[]
            {
                new ReturnRequestLine(LineId(sale, _coffeeId), 1),
                new ReturnRequestLine(LineId(sale, _waterId), 1)
            }));

            AssertNoReturnRecorded();
            Assert.Equal(5, Stock(_coffeeId));
        }

        [Fact]
        public void A_returned_article_of_an_archived_product_still_goes_back_in_stock()
        {
            var sale = Sell(coffee: 5, water: 2);
            _products.SetArchived(_coffeeId, true);

            _service.ReturnItems(sale.Id, new[] { new ReturnRequestLine(LineId(sale, _coffeeId), 5) });

            Assert.Equal(10, Stock(_coffeeId));
        }

        // Consultation.

        [Fact]
        public void Sales_are_listed_for_a_period_of_local_days_the_latest_first()
        {
            var today = _now.ToLocalTime().Date;
            var first = Sell(coffee: 1, water: 2);
            _now = _now.AddMinutes(5);
            var second = Sell(coffee: 2, water: 0);
            _service.CancelSale(second.Id);
            _now = _now.AddDays(1);
            var nextDay = Sell(coffee: 1, water: 0);

            var listed = _service.SearchSales(today, today, null);

            Assert.Equal(new[] { second.Number, first.Number }, listed.Select(s => s.Number));
            Assert.Equal(new[] { 2, 3 }, listed.Select(s => s.ArticleCount));
            Assert.Equal(new long[] { 104100, 59050 }, listed.Select(s => s.TotalCents));
            Assert.Equal(new[] { Sale.StatusCancelled, Sale.StatusValidated }, listed.Select(s => s.Status));
            Assert.Equal(first.CreatedAtUtc, listed[1].CreatedAtUtc);
            Assert.Equal(DateTimeKind.Utc, listed[1].CreatedAtUtc.Kind);

            Assert.Equal(new[] { nextDay.Number }, _service.SearchSales(today.AddDays(1), today.AddDays(1), null).Select(s => s.Number));
            Assert.Equal(3, _service.SearchSales(today, today.AddDays(1), null).Count);
            Assert.Empty(_service.SearchSales(today.AddDays(-2), today.AddDays(-1), null));
        }

        [Fact]
        public void A_number_is_searched_in_every_period_ignoring_case()
        {
            var today = _now.ToLocalTime().Date;
            var first = Sell(coffee: 1, water: 0);
            var second = Sell(coffee: 1, water: 0);
            var farAway = today.AddYears(-1);

            Assert.Equal(new[] { second.Number }, _service.SearchSales(farAway, farAway, "-0002").Select(s => s.Number));
            Assert.Equal(new[] { first.Number }, _service.SearchSales(farAway, farAway, first.Number.ToLowerInvariant()).Select(s => s.Number));
            Assert.Equal(2, _service.SearchSales(farAway, farAway, "V-").Count);
            Assert.Empty(_service.SearchSales(today, today, "V-1999"));
        }

        [Fact]
        public void A_sale_is_read_with_its_lines_and_its_returns()
        {
            var sale = Sell(coffee: 5, water: 2);
            _service.ReturnItems(sale.Id, new[] { new ReturnRequestLine(LineId(sale, _waterId), 1) });

            var read = _service.GetSale(sale.Id);

            Assert.Equal(sale.Number, read.Number);
            Assert.Equal(sale.CreatedAtUtc, read.CreatedAtUtc);
            Assert.Equal(60000 * 5, read.ReceivedCents);
            Assert.Equal(new[] { "Café moulu", "Eau minérale" }, read.Lines.Select(l => l.ProductName));
            Assert.Equal(new long[] { 260250, 7000 }, read.Lines.Select(l => l.LineTotalCents));
            Assert.Equal(new long[] { 40000, 2500 }, read.Lines.Select(l => l.UnitPurchasePriceCents));
            Assert.Equal(new[] { 0, 1 }, read.Lines.Select(l => l.ReturnedQuantity));

            var saleReturn = _service.GetReturns(sale.Id).Single();
            Assert.Equal(sale.Id, saleReturn.SaleId);
            Assert.Equal("Eau minérale", saleReturn.Lines.Single().ProductName);
            Assert.Equal(3500, saleReturn.RefundCents);

            Assert.Null(_service.GetSale(999));
            Assert.Empty(_service.GetReturns(999));
        }

        private Sale Sell(int coffee, int water)
        {
            var cart = new Cart();
            if (coffee > 0)
            {
                _service.AddProduct(cart, _coffeeId);
                _service.SetQuantity(cart, _coffeeId, coffee);
            }

            if (water > 0)
            {
                _service.AddProduct(cart, _waterId);
                _service.SetQuantity(cart, _waterId, water);
            }

            return _service.Validate(cart, 60000 * 5);
        }

        private long LineId(Sale sale, long productId)
        {
            return _service.GetSale(sale.Id).Lines.Single(line => line.ProductId == productId).Id;
        }

        private long Stock(long productId)
        {
            return _products.GetById(productId).StockQuantity;
        }

        private int Count(string tableAndFilter)
        {
            using (var connection = _database.Open())
            {
                // The text comes from the test itself, never from user input.
                return connection.ExecuteScalar<int>("SELECT COUNT(*) FROM " + tableAndFilter);
            }
        }

        private void AssertNoReturnRecorded()
        {
            Assert.Equal(0, Count("returns"));
            Assert.Equal(0, Count("return_lines"));
            Assert.Equal(0, Count("stock_movements WHERE type = 'RETOUR'"));
        }
    }
}
