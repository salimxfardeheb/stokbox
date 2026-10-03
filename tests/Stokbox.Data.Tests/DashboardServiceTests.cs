using System;
using System.Diagnostics;
using System.Linq;
using Dapper;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;
using Stokbox.Data.Migrations;
using Stokbox.Data.Repositories;
using Xunit;

namespace Stokbox.Data.Tests
{
    /// <summary>
    /// Every expected value below is computed by hand from the data built in the test.
    /// </summary>
    public class DashboardServiceTests : IDisposable
    {
        // The period under test: one local day, with the day before and the day after around it.
        private static readonly DateTime Day = new DateTime(2026, 10, 3);

        private readonly TempDatabase _database = new TempDatabase();
        private readonly ProductRepository _products;
        private readonly StockMovementRepository _movements;
        private readonly SaleService _sales;
        private readonly SqliteDashboardService _dashboard;
        private readonly long _groceryId;
        private readonly long _coffeeId;
        private readonly long _waterId;
        private readonly long _teaId;

        private DateTime _now = LocalTime(Day, 9, 0);

        public DashboardServiceTests()
        {
            new MigrationRunner(_database.ConnectionFactory).MigrateToLatest();
            _products = new ProductRepository(_database.ConnectionFactory);
            _movements = new StockMovementRepository(_database.ConnectionFactory);
            _sales = new SaleService(_products, new SaleRepository(_database.ConnectionFactory), () => _now);
            _dashboard = new SqliteDashboardService(_database.ConnectionFactory);

            var categories = new CategoryRepository(_database.ConnectionFactory);
            _groceryId = categories.Insert("Épicerie");
            var drinksId = categories.Insert("Boissons");

            // Coffee: bought 400,00 DA, sold 520,50 DA, 10 in stock.
            _coffeeId = _products.Insert("2000000000015", "Café moulu", _groceryId, 40000, 52050, _now);
            _movements.AddEntry(_coffeeId, 10, _now);

            // Water: bought 25,00 DA, sold 35,00 DA, 24 in stock.
            _waterId = _products.Insert("2000000000022", "Eau minérale", drinksId, 2500, 3500, _now);
            _movements.AddEntry(_waterId, 24, _now);

            // Tea: bought 100,00 DA, sold 150,00 DA, 4 in stock, archived.
            _teaId = _products.Insert("2000000000039", "Thé vert", _groceryId, 10000, 15000, _now);
            _movements.AddEntry(_teaId, 4, _now);
            _products.SetArchived(_teaId, true);

            // Sugar: active, never received. Old product: archived, never received.
            _products.Insert("2000000000046", "Sucre", _groceryId, 5000, 7000, _now);
            var oldId = _products.Insert("2000000000053", "Ancien produit", _groceryId, 5000, 7000, _now);
            _products.SetArchived(oldId, true);
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        // Stock.

        [Fact]
        public void Stock_values_include_archived_products_with_stock_and_leave_out_empty_ones()
        {
            var stock = _dashboard.GetStockSummary();

            // 10 x 400,00 + 24 x 25,00 + 4 x 100,00
            Assert.Equal(500000, stock.PurchaseValueCents);

            // 10 x 520,50 + 24 x 35,00 + 4 x 150,00
            Assert.Equal(664500, stock.SaleValueCents);
            Assert.Equal(164500, stock.PotentialMarginCents);
            Assert.Equal(3, stock.ReferenceCount);
            Assert.Equal(38, stock.UnitCount);

            // Sugar only: the old product has no stock either but is archived.
            Assert.Equal(1, stock.OutOfStockCount);
        }

        [Fact]
        public void A_product_sold_out_leaves_the_values_and_counts_as_out_of_stock()
        {
            Sell(coffee: 10, water: 4);

            var stock = _dashboard.GetStockSummary();

            // 20 x 25,00 + 4 x 100,00, then 20 x 35,00 + 4 x 150,00
            Assert.Equal(90000, stock.PurchaseValueCents);
            Assert.Equal(130000, stock.SaleValueCents);
            Assert.Equal(2, stock.ReferenceCount);
            Assert.Equal(24, stock.UnitCount);
            Assert.Equal(2, stock.OutOfStockCount);
        }

        [Fact]
        public void Stock_values_follow_the_current_prices()
        {
            _products.Update(_coffeeId, "Café moulu", _groceryId, 45000, 60000);

            var stock = _dashboard.GetStockSummary();

            Assert.Equal(550000, stock.PurchaseValueCents);
            Assert.Equal(744000, stock.SaleValueCents);
        }

        [Fact]
        public void An_empty_database_has_an_empty_stock()
        {
            using (var empty = new TempDatabase())
            {
                new MigrationRunner(empty.ConnectionFactory).MigrateToLatest();

                var stock = new SqliteDashboardService(empty.ConnectionFactory).GetStockSummary();

                Assert.Equal(0, stock.PurchaseValueCents);
                Assert.Equal(0, stock.SaleValueCents);
                Assert.Equal(0, stock.ReferenceCount);
                Assert.Equal(0, stock.UnitCount);
                Assert.Equal(0, stock.OutOfStockCount);
            }
        }

        [Fact]
        public void The_out_of_stock_filter_of_the_products_lists_what_the_dashboard_counts()
        {
            Sell(coffee: 10, water: 4);

            var listed = _products.Search(new Core.Repositories.ProductSearchCriteria { OutOfStockOnly = true, IncludeArchived = true });

            Assert.Equal(new[] { "Café moulu", "Sucre" }, listed.Select(p => p.Name));
            Assert.Equal(_dashboard.GetStockSummary().OutOfStockCount, listed.Count);
        }

        // Ventes.

        [Fact]
        public void Kpis_of_a_day_with_one_sale()
        {
            Sell(coffee: 5, water: 2);

            var kpis = _dashboard.GetSalesKpis(Day, Day);

            Assert.Equal(1, kpis.SaleCount);

            // 5 x 520,50 + 2 x 35,00
            Assert.Equal(267250, kpis.NetRevenueCents);

            // 5 x 400,00 + 2 x 25,00
            Assert.Equal(205000, kpis.NetCostCents);
            Assert.Equal(62250, kpis.GrossProfitCents);

            // 62 250 / 267 250 = 23,29 %
            Assert.Equal(23.3m, kpis.MarginRatePercent);
            Assert.Equal(267250, kpis.AverageBasketCents);
            Assert.Equal(7, kpis.NetItemCount);
        }

        [Fact]
        public void A_cancelled_sale_counts_nowhere()
        {
            Sell(coffee: 5, water: 2);
            _now = LocalTime(Day, 11, 0);
            var cancelled = Sell(coffee: 1, water: 6);
            _sales.CancelSale(cancelled.Id);

            var kpis = _dashboard.GetSalesKpis(Day, Day);
            Assert.Equal(1, kpis.SaleCount);
            Assert.Equal(267250, kpis.NetRevenueCents);
            Assert.Equal(205000, kpis.NetCostCents);
            Assert.Equal(267250, kpis.AverageBasketCents);
            Assert.Equal(7, kpis.NetItemCount);

            Assert.Equal(new long[] { 5, 2 }, _dashboard.GetTopProducts(Day, Day).Select(p => p.NetQuantity));
            Assert.Equal(new long[] { 260250, 7000 }, _dashboard.GetSalesByCategory(Day, Day).Select(c => c.NetRevenueCents));
            Assert.Equal(267250, _dashboard.GetDailySales(Day, Day).Single().NetRevenueCents);

            // Its articles are back in stock.
            Assert.Equal(5 + 22 + 4, _dashboard.GetStockSummary().UnitCount);
        }

        [Fact]
        public void A_partial_return_reduces_revenue_cost_profit_and_articles_but_not_the_basket()
        {
            var sale = Sell(coffee: 5, water: 2);
            _now = LocalTime(Day, 15, 0);
            _sales.ReturnItems(sale.Id, new[] { new ReturnRequestLine(LineId(sale, _coffeeId), 2) });

            var kpis = _dashboard.GetSalesKpis(Day, Day);

            // 267 250 - 2 x 520,50
            Assert.Equal(163150, kpis.NetRevenueCents);

            // 205 000 - 2 x 400,00
            Assert.Equal(125000, kpis.NetCostCents);
            Assert.Equal(38150, kpis.GrossProfitCents);

            // 38 150 / 163 150 = 23,38 %
            Assert.Equal(23.4m, kpis.MarginRatePercent);
            Assert.Equal(5, kpis.NetItemCount);

            // The number of sales and the average basket are those before returns.
            Assert.Equal(1, kpis.SaleCount);
            Assert.Equal(267250, kpis.AverageBasketCents);
        }

        [Fact]
        public void A_return_counts_in_the_period_it_was_made_in()
        {
            var sale = Sell(coffee: 5, water: 2);
            _now = LocalTime(Day.AddDays(1), 10, 0);
            _sales.ReturnItems(sale.Id, new[] { new ReturnRequestLine(LineId(sale, _coffeeId), 2) });

            // The day of the sale is untouched.
            var saleDay = _dashboard.GetSalesKpis(Day, Day);
            Assert.Equal(267250, saleDay.NetRevenueCents);
            Assert.Equal(7, saleDay.NetItemCount);

            // The day after carries the return alone.
            var returnDay = _dashboard.GetSalesKpis(Day.AddDays(1), Day.AddDays(1));
            Assert.Equal(0, returnDay.SaleCount);
            Assert.Equal(-104100, returnDay.NetRevenueCents);
            Assert.Equal(-80000, returnDay.NetCostCents);
            Assert.Equal(-2, returnDay.NetItemCount);
            Assert.Null(returnDay.AverageBasketCents);

            // Both days together.
            var both = _dashboard.GetSalesKpis(Day, Day.AddDays(1));
            Assert.Equal(163150, both.NetRevenueCents);
            Assert.Equal(125000, both.NetCostCents);
            Assert.Equal(5, both.NetItemCount);

            Assert.Equal(
                new long[] { 267250, -104100 },
                _dashboard.GetDailySales(Day, Day.AddDays(1)).Select(d => d.NetRevenueCents));
        }

        [Fact]
        public void A_price_change_after_the_sale_changes_nothing_to_its_kpis()
        {
            var sale = Sell(coffee: 5, water: 2);
            _products.Update(_coffeeId, "Café moulu", _groceryId, 45000, 60000);
            _products.Update(_waterId, "Eau minérale", _groceryId, 3000, 5000);

            var kpis = _dashboard.GetSalesKpis(Day, Day);
            Assert.Equal(267250, kpis.NetRevenueCents);
            Assert.Equal(205000, kpis.NetCostCents);
            Assert.Equal(62250, kpis.GrossProfitCents);

            // A later return is valued at the frozen prices too (RG-04).
            _now = LocalTime(Day, 15, 0);
            _sales.ReturnItems(sale.Id, new[] { new ReturnRequestLine(LineId(sale, _coffeeId), 2) });

            kpis = _dashboard.GetSalesKpis(Day, Day);
            Assert.Equal(163150, kpis.NetRevenueCents);
            Assert.Equal(125000, kpis.NetCostCents);
            Assert.Equal(156150, _dashboard.GetTopProducts(Day, Day)[0].NetRevenueCents);
        }

        [Fact]
        public void A_sale_made_at_23h30_local_time_counts_on_its_own_day()
        {
            _now = LocalTime(Day, 23, 30);
            Sell(coffee: 1, water: 0);
            _now = LocalTime(Day.AddDays(1), 0, 10);
            Sell(coffee: 0, water: 1);

            Assert.Equal(52050, _dashboard.GetSalesKpis(Day, Day).NetRevenueCents);
            Assert.Equal(3500, _dashboard.GetSalesKpis(Day.AddDays(1), Day.AddDays(1)).NetRevenueCents);
            Assert.Equal(0, _dashboard.GetSalesKpis(Day.AddDays(-1), Day.AddDays(-1)).SaleCount);

            var days = _dashboard.GetDailySales(Day.AddDays(-1), Day.AddDays(1));
            Assert.Equal(new[] { Day.AddDays(-1), Day, Day.AddDays(1) }, days.Select(d => d.Date));
            Assert.Equal(new long[] { 0, 52050, 3500 }, days.Select(d => d.NetRevenueCents));
        }

        [Fact]
        public void Margin_rate_and_average_basket_have_no_value_without_sales()
        {
            var kpis = _dashboard.GetSalesKpis(Day, Day);

            Assert.Equal(0, kpis.SaleCount);
            Assert.Equal(0, kpis.NetRevenueCents);
            Assert.Equal(0, kpis.NetCostCents);
            Assert.Equal(0, kpis.GrossProfitCents);
            Assert.Equal(0, kpis.NetItemCount);
            Assert.Null(kpis.MarginRatePercent);
            Assert.Null(kpis.AverageBasketCents);
            Assert.Empty(_dashboard.GetTopProducts(Day, Day));
            Assert.Empty(_dashboard.GetSalesByCategory(Day, Day));
        }

        [Fact]
        public void Margin_rate_has_no_value_when_everything_sold_was_returned()
        {
            var sale = Sell(coffee: 1, water: 0);
            _sales.ReturnItems(sale.Id, new[] { new ReturnRequestLine(LineId(sale, _coffeeId), 1) });

            var kpis = _dashboard.GetSalesKpis(Day, Day);

            Assert.Equal(0, kpis.NetRevenueCents);
            Assert.Null(kpis.MarginRatePercent);

            // The sale itself still counts, and so does its basket.
            Assert.Equal(1, kpis.SaleCount);
            Assert.Equal(52050, kpis.AverageBasketCents);
            Assert.Empty(_dashboard.GetTopProducts(Day, Day));
        }

        [Fact]
        public void The_average_basket_is_the_total_of_the_sales_divided_by_their_number()
        {
            Sell(coffee: 5, water: 2);
            _now = LocalTime(Day, 16, 0);
            Sell(coffee: 0, water: 3);

            var kpis = _dashboard.GetSalesKpis(Day, Day);

            Assert.Equal(2, kpis.SaleCount);

            // (267 250 + 10 500) / 2
            Assert.Equal(138875, kpis.AverageBasketCents);
        }

        [Fact]
        public void Top_products_and_categories_are_net_of_returns_the_best_first()
        {
            var sale = Sell(coffee: 5, water: 2);
            _now = LocalTime(Day, 15, 0);
            _sales.ReturnItems(sale.Id, new[] { new ReturnRequestLine(LineId(sale, _coffeeId), 2) });

            var top = _dashboard.GetTopProducts(Day, Day);
            Assert.Equal(new[] { "Café moulu", "Eau minérale" }, top.Select(p => p.Name));
            Assert.Equal(new long[] { 3, 2 }, top.Select(p => p.NetQuantity));
            Assert.Equal(new long[] { 156150, 7000 }, top.Select(p => p.NetRevenueCents));

            Assert.Equal("Café moulu", _dashboard.GetTopProducts(Day, Day, 1).Single().Name);

            var categories = _dashboard.GetSalesByCategory(Day, Day);
            Assert.Equal(new[] { "Épicerie", "Boissons" }, categories.Select(c => c.CategoryName));
            Assert.Equal(new long[] { 156150, 7000 }, categories.Select(c => c.NetRevenueCents));

            // The tables add up to the net revenue of the period.
            Assert.Equal(_dashboard.GetSalesKpis(Day, Day).NetRevenueCents, categories.Sum(c => c.NetRevenueCents));
        }

        [Fact]
        public void Every_day_of_the_period_is_listed_even_without_sales()
        {
            Sell(coffee: 1, water: 0);

            var days = _dashboard.GetDailySales(Day.AddDays(-400), Day.AddDays(2));

            Assert.Equal(403, days.Count);
            Assert.Equal(Day.AddDays(-400), days[0].Date);
            Assert.Equal(Day.AddDays(2), days[402].Date);
            Assert.Equal(52050, days[400].NetRevenueCents);
            Assert.Equal(52050, days.Sum(d => d.NetRevenueCents));
        }

        [Fact]
        public void The_dashboard_writes_nothing()
        {
            Sell(coffee: 5, water: 2);
            long changesBefore;
            using (var connection = _database.Open())
            {
                changesBefore = connection.ExecuteScalar<long>("PRAGMA data_version");
                _dashboard.GetStockSummary();
                _dashboard.GetSalesKpis(Day, Day);
                _dashboard.GetTopProducts(Day, Day);
                _dashboard.GetSalesByCategory(Day, Day);
                _dashboard.GetDailySales(Day, Day);

                // data_version changes as soon as another connection commits a write.
                Assert.Equal(changesBefore, connection.ExecuteScalar<long>("PRAGMA data_version"));
            }
        }

        // Performance.

        [Fact]
        [Trait("Category", "Performance")]
        public void The_whole_dashboard_is_computed_in_less_than_2_seconds_on_10000_products_and_50000_sales()
        {
            using (var large = new TempDatabase())
            {
                new MigrationRunner(large.ConnectionFactory).MigrateToLatest();
                var today = DateTime.Today;
                SeedLargeDatabase(large, today);
                var dashboard = new SqliteDashboardService(large.ConnectionFactory);

                // The periods offered by the screen, the widest being about two months of sales.
                var periods = new[]
                {
                    Tuple.Create(today, today),
                    Tuple.Create(today.AddDays(-6), today),
                    Tuple.Create(new DateTime(today.Year, today.Month, 1).AddMonths(-1), today)
                };

                foreach (var period in periods)
                {
                    var watch = Stopwatch.StartNew();
                    dashboard.GetStockSummary();
                    var kpis = dashboard.GetSalesKpis(period.Item1, period.Item2);
                    dashboard.GetTopProducts(period.Item1, period.Item2);
                    dashboard.GetSalesByCategory(period.Item1, period.Item2);
                    dashboard.GetDailySales(period.Item1, period.Item2);
                    watch.Stop();

                    Assert.True(kpis.SaleCount > 0);
                    Assert.True(
                        watch.ElapsedMilliseconds < 2000,
                        "Tableau de bord calculé en " + watch.ElapsedMilliseconds + " ms.");
                }

                using (var connection = large.Open())
                {
                    Assert.Equal(10000, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM products"));
                    Assert.Equal(50000, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sales"));
                }
            }
        }

        // 10 000 products, 50 000 sales of two lines spread over the last year, 2 % cancelled, 4 % with a return.
        private static void SeedLargeDatabase(TempDatabase database, DateTime today)
        {
            const string Numbers = "WITH RECURSIVE n (i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < @Count) ";

            // One sale every 631 seconds: 50 000 sales cover 365 days and end today.
            const string SaleDate = "strftime('%Y-%m-%dT%H:%M:%fZ', @Start, '+' || (i * 631) || ' seconds')";
            var start = today.AddDays(-365).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");

            using (var connection = database.Open())
            using (var transaction = connection.BeginTransaction())
            {
                connection.Execute(
                    Numbers + "INSERT INTO categories (id, name) SELECT i, 'Catégorie ' || i FROM n",
                    new { Count = 20 },
                    transaction);
                connection.Execute(
                    Numbers + "INSERT INTO products (id, barcode, name, category_id, purchase_price_cents, sale_price_cents, is_archived, created_at) " +
                    "SELECT i, printf('%013d', i), 'Produit ' || i, 1 + i % 20, 1000 + i % 500, 1600 + i % 900, 0, '2025-01-01T00:00:00.000Z' FROM n",
                    new { Count = 10000 },
                    transaction);
                connection.Execute(
                    "INSERT INTO stock_movements (product_id, type, quantity, created_at) " +
                    "SELECT id, 'ENTREE', 1000, '2025-01-01T00:00:00.000Z' FROM products",
                    transaction: transaction);
                connection.Execute(
                    Numbers + "INSERT INTO sales (id, number, created_at, total_cents, received_cents, change_cents, status) " +
                    "SELECT i, 'V-' || i, " + SaleDate + ", 0, 0, 0, CASE WHEN i % 50 = 0 THEN 'ANNULEE' ELSE 'VALIDEE' END FROM n",
                    new { Count = 50000, Start = start },
                    transaction);
                connection.Execute(
                    "INSERT INTO sale_lines (sale_id, product_id, quantity, unit_price_cents, unit_purchase_price_cents, line_total_cents) " +
                    "SELECT s.id, p.id, 1 + s.id % 3, p.sale_price_cents, p.purchase_price_cents, (1 + s.id % 3) * p.sale_price_cents " +
                    "FROM sales s JOIN products p ON p.id = 1 + (s.id * 7) % 10000 " +
                    "UNION ALL " +
                    "SELECT s.id, p.id, 1, p.sale_price_cents, p.purchase_price_cents, p.sale_price_cents " +
                    "FROM sales s JOIN products p ON p.id = 1 + (s.id * 13) % 10000",
                    transaction: transaction);
                connection.Execute(
                    "INSERT INTO stock_movements (product_id, type, quantity, sale_id, created_at) " +
                    "SELECT l.product_id, 'VENTE', -l.quantity, l.sale_id, s.created_at FROM sale_lines l JOIN sales s ON s.id = l.sale_id",
                    transaction: transaction);
                connection.Execute(
                    "INSERT INTO stock_movements (product_id, type, quantity, sale_id, created_at) " +
                    "SELECT l.product_id, 'ANNULATION', l.quantity, l.sale_id, s.created_at " +
                    "FROM sale_lines l JOIN sales s ON s.id = l.sale_id WHERE s.status = 'ANNULEE'",
                    transaction: transaction);

                // One article of the first line comes back, the same day.
                connection.Execute(
                    "INSERT INTO returns (id, sale_id, created_at) SELECT id, id, created_at FROM sales WHERE id % 25 = 1",
                    transaction: transaction);
                connection.Execute(
                    "INSERT INTO return_lines (return_id, sale_line_id, quantity) " +
                    "SELECT r.id, MIN(l.id), 1 FROM returns r JOIN sale_lines l ON l.sale_id = r.sale_id GROUP BY r.id",
                    transaction: transaction);
                connection.Execute(
                    "INSERT INTO stock_movements (product_id, type, quantity, sale_id, created_at) " +
                    "SELECT l.product_id, 'RETOUR', rl.quantity, l.sale_id, r.created_at " +
                    "FROM return_lines rl JOIN returns r ON r.id = rl.return_id JOIN sale_lines l ON l.id = rl.sale_line_id",
                    transaction: transaction);

                transaction.Commit();
            }
        }

        private static DateTime LocalTime(DateTime day, int hour, int minute)
        {
            return new DateTime(day.Year, day.Month, day.Day, hour, minute, 0, DateTimeKind.Local).ToUniversalTime();
        }

        private Sale Sell(int coffee, int water)
        {
            var cart = new Cart();
            if (coffee > 0)
            {
                _sales.AddProduct(cart, _coffeeId);
                _sales.SetQuantity(cart, _coffeeId, coffee);
            }

            if (water > 0)
            {
                _sales.AddProduct(cart, _waterId);
                _sales.SetQuantity(cart, _waterId, water);
            }

            return _sales.Validate(cart, 10000000);
        }

        private long LineId(Sale sale, long productId)
        {
            return _sales.GetSale(sale.Id).Lines.Single(line => line.ProductId == productId).Id;
        }
    }
}
