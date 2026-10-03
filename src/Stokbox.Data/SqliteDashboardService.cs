using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Dapper;
using Stokbox.Core.Dashboard;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;

namespace Stokbox.Data
{
    /// <summary>
    /// Dashboard indicators, each computed by SQL aggregation. Never writes to the database.
    /// </summary>
    public sealed class SqliteDashboardService : IDashboardService
    {
        // Days sent to one daily query (3 parameters each), well under the parameter limit of SQLite.
        private const int DaysPerQuery = 300;

        // Lines sold by the validated sales of the period.
        private const string SoldLinesSql =
            "FROM sales s JOIN sale_lines l ON l.sale_id = s.id " +
            "WHERE s.status = @Status AND s.created_at >= @From AND s.created_at < @To ";

        // Lines returned during the period, whatever the date of their sale. The prices are those of the sale line (RG-04).
        private const string ReturnedLinesSql =
            "FROM returns r JOIN sales s ON s.id = r.sale_id " +
            "JOIN return_lines rl ON rl.return_id = r.id " +
            "JOIN sale_lines l ON l.id = rl.sale_line_id " +
            "WHERE s.status = @Status AND r.created_at >= @From AND r.created_at < @To ";

        // Sold lines counted positively and returned lines negatively, per product.
        private const string NetLinesSql =
            "(SELECT l.product_id AS product_id, l.quantity AS quantity, l.line_total_cents AS revenue_cents " + SoldLinesSql +
            "UNION ALL " +
            "SELECT l.product_id, -rl.quantity, -rl.quantity * l.unit_price_cents " + ReturnedLinesSql + ") x ";

        private readonly SqliteConnectionFactory _connectionFactory;

        public SqliteDashboardService(SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public StockSummary GetStockSummary()
        {
            using (var connection = _connectionFactory.Open())
            {
                // The quantity of a product is the sum of its movements (RG-01).
                var row = connection.QuerySingle<StockRow>(
                    "SELECT " +
                    "COALESCE(SUM(CASE WHEN q.quantity > 0 THEN q.quantity * q.purchase_price_cents END), 0) AS PurchaseValueCents, " +
                    "COALESCE(SUM(CASE WHEN q.quantity > 0 THEN q.quantity * q.sale_price_cents END), 0) AS SaleValueCents, " +
                    "COALESCE(SUM(CASE WHEN q.quantity > 0 THEN 1 END), 0) AS ReferenceCount, " +
                    "COALESCE(SUM(CASE WHEN q.quantity > 0 THEN q.quantity END), 0) AS UnitCount, " +
                    "COALESCE(SUM(CASE WHEN q.quantity = 0 AND q.is_archived = 0 THEN 1 END), 0) AS OutOfStockCount " +
                    "FROM (SELECT p.purchase_price_cents, p.sale_price_cents, p.is_archived, COALESCE(m.quantity, 0) AS quantity " +
                    "FROM products p " +
                    "LEFT JOIN (SELECT product_id, SUM(quantity) AS quantity FROM stock_movements GROUP BY product_id) m " +
                    "ON m.product_id = p.id) q");

                return new StockSummary(row.PurchaseValueCents, row.SaleValueCents, row.ReferenceCount, row.UnitCount, row.OutOfStockCount);
            }
        }

        public SalesKpis GetSalesKpis(DateTime fromLocalDate, DateTime toLocalDate)
        {
            var period = PeriodParameters(fromLocalDate, toLocalDate);

            using (var connection = _connectionFactory.Open())
            {
                var sold = connection.QuerySingle<TotalsRow>(
                    "SELECT COUNT(DISTINCT s.id) AS SaleCount, " +
                    "COALESCE(SUM(l.line_total_cents), 0) AS RevenueCents, " +
                    "COALESCE(SUM(l.quantity * l.unit_purchase_price_cents), 0) AS CostCents, " +
                    "COALESCE(SUM(l.quantity), 0) AS Quantity " + SoldLinesSql,
                    period);

                var returned = connection.QuerySingle<TotalsRow>(
                    "SELECT COALESCE(SUM(rl.quantity * l.unit_price_cents), 0) AS RevenueCents, " +
                    "COALESCE(SUM(rl.quantity * l.unit_purchase_price_cents), 0) AS CostCents, " +
                    "COALESCE(SUM(rl.quantity), 0) AS Quantity " + ReturnedLinesSql,
                    period);

                return new SalesKpis(
                    sold.SaleCount,
                    sold.RevenueCents,
                    sold.RevenueCents - returned.RevenueCents,
                    sold.CostCents - returned.CostCents,
                    sold.Quantity - returned.Quantity);
            }
        }

        public IReadOnlyList<TopProduct> GetTopProducts(DateTime fromLocalDate, DateTime toLocalDate, int limit = 10)
        {
            if (limit < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }

            var period = PeriodParameters(fromLocalDate, toLocalDate);
            period.Add("Limit", limit);

            using (var connection = _connectionFactory.Open())
            {
                return connection
                    .Query<ProductRow>(
                        "SELECT p.name AS Name, SUM(x.quantity) AS NetQuantity, SUM(x.revenue_cents) AS NetRevenueCents " +
                        "FROM " + NetLinesSql +
                        "JOIN products p ON p.id = x.product_id " +
                        "GROUP BY p.id, p.name " +
                        "HAVING SUM(x.quantity) <> 0 OR SUM(x.revenue_cents) <> 0 " +
                        "ORDER BY NetRevenueCents DESC, NetQuantity DESC, fold(p.name), p.id " +
                        "LIMIT @Limit",
                        period)
                    .Select(row => new TopProduct(row.Name, row.NetQuantity, row.NetRevenueCents))
                    .ToList();
            }
        }

        public IReadOnlyList<CategorySales> GetSalesByCategory(DateTime fromLocalDate, DateTime toLocalDate)
        {
            var period = PeriodParameters(fromLocalDate, toLocalDate);

            using (var connection = _connectionFactory.Open())
            {
                return connection
                    .Query<CategoryRow>(
                        "SELECT c.name AS CategoryName, SUM(x.revenue_cents) AS NetRevenueCents " +
                        "FROM " + NetLinesSql +
                        "JOIN products p ON p.id = x.product_id " +
                        "LEFT JOIN categories c ON c.id = p.category_id " +
                        "GROUP BY c.id, c.name " +
                        "ORDER BY NetRevenueCents DESC, fold(c.name), c.id",
                        period)
                    .Select(row => new CategorySales(row.CategoryName, row.NetRevenueCents))
                    .ToList();
            }
        }

        public IReadOnlyList<DailySales> GetDailySales(DateTime fromLocalDate, DateTime toLocalDate)
        {
            var first = FirstDay(fromLocalDate, toLocalDate);
            var dayCount = (int)(LastDay(fromLocalDate, toLocalDate) - first).TotalDays + 1;
            var result = new List<DailySales>(dayCount);

            using (var connection = _connectionFactory.Open())
            {
                for (var offset = 0; offset < dayCount; offset += DaysPerQuery)
                {
                    var count = Math.Min(DaysPerQuery, dayCount - offset);
                    var parameters = new DynamicParameters();
                    parameters.Add("Status", Sale.StatusValidated);

                    // Each local day is sent with its own UTC bounds: a day is not always 24 hours long (time changes).
                    var days = new StringBuilder();
                    for (var i = 0; i < count; i++)
                    {
                        var day = first.AddDays(offset + i);
                        var suffix = i.ToString(CultureInfo.InvariantCulture);
                        days.Append(i == 0 ? "(" : ", (")
                            .Append("@Day").Append(suffix)
                            .Append(", @From").Append(suffix)
                            .Append(", @To").Append(suffix)
                            .Append(")");
                        parameters.Add("Day" + suffix, offset + i);
                        parameters.Add("From" + suffix, SqliteDates.ToText(ToUtc(day)));
                        parameters.Add("To" + suffix, SqliteDates.ToText(ToUtc(day.AddDays(1))));
                    }

                    var rows = connection.Query<DayRow>(
                        "WITH days (day_index, from_utc, to_utc) AS (VALUES " + days + ") " +
                        "SELECT d.day_index AS DayIndex, " +
                        "COALESCE((SELECT SUM(l.line_total_cents) FROM sales s JOIN sale_lines l ON l.sale_id = s.id " +
                        "WHERE s.status = @Status AND s.created_at >= d.from_utc AND s.created_at < d.to_utc), 0) " +
                        "- COALESCE((SELECT SUM(rl.quantity * l.unit_price_cents) FROM returns r " +
                        "JOIN sales s ON s.id = r.sale_id " +
                        "JOIN return_lines rl ON rl.return_id = r.id " +
                        "JOIN sale_lines l ON l.id = rl.sale_line_id " +
                        "WHERE s.status = @Status AND r.created_at >= d.from_utc AND r.created_at < d.to_utc), 0) AS NetRevenueCents " +
                        "FROM days d ORDER BY d.day_index",
                        parameters);

                    result.AddRange(rows.Select(row => new DailySales(first.AddDays(row.DayIndex), row.NetRevenueCents)));
                }
            }

            return result;
        }

        // Local bounds [first day 00:00, day after the last 00:00[ converted to UTC, as the dates are stored.
        private static DynamicParameters PeriodParameters(DateTime fromLocalDate, DateTime toLocalDate)
        {
            var parameters = new DynamicParameters();
            parameters.Add("Status", Sale.StatusValidated);
            parameters.Add("From", SqliteDates.ToText(ToUtc(FirstDay(fromLocalDate, toLocalDate))));
            parameters.Add("To", SqliteDates.ToText(ToUtc(LastDay(fromLocalDate, toLocalDate).AddDays(1))));
            return parameters;
        }

        private static DateTime FirstDay(DateTime from, DateTime to)
        {
            return from <= to ? from.Date : to.Date;
        }

        private static DateTime LastDay(DateTime from, DateTime to)
        {
            return from <= to ? to.Date : from.Date;
        }

        private static DateTime ToUtc(DateTime localMidnight)
        {
            return DateTime.SpecifyKind(localMidnight, DateTimeKind.Local).ToUniversalTime();
        }

        private sealed class StockRow
        {
            public long PurchaseValueCents { get; set; }

            public long SaleValueCents { get; set; }

            public long ReferenceCount { get; set; }

            public long UnitCount { get; set; }

            public long OutOfStockCount { get; set; }
        }

        private sealed class TotalsRow
        {
            public long SaleCount { get; set; }

            public long RevenueCents { get; set; }

            public long CostCents { get; set; }

            public long Quantity { get; set; }
        }

        private sealed class ProductRow
        {
            public string Name { get; set; }

            public long NetQuantity { get; set; }

            public long NetRevenueCents { get; set; }
        }

        private sealed class CategoryRow
        {
            public string CategoryName { get; set; }

            public long NetRevenueCents { get; set; }
        }

        private sealed class DayRow
        {
            public int DayIndex { get; set; }

            public long NetRevenueCents { get; set; }
        }
    }
}
