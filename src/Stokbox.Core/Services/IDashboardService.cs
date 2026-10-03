using System;
using System.Collections.Generic;
using Stokbox.Core.Dashboard;

namespace Stokbox.Core.Services
{
    /// <summary>
    /// Read-only indicators computed from the existing data. A period is made of whole local days,
    /// both included; only the date part of the bounds is used.
    /// </summary>
    public interface IDashboardService
    {
        StockSummary GetStockSummary();

        SalesKpis GetSalesKpis(DateTime fromLocalDate, DateTime toLocalDate);

        /// <summary>
        /// Products with the highest net revenue over the period, the best first.
        /// </summary>
        IReadOnlyList<TopProduct> GetTopProducts(DateTime fromLocalDate, DateTime toLocalDate, int limit = 10);

        /// <summary>
        /// Net revenue per category, the highest first.
        /// </summary>
        IReadOnlyList<CategorySales> GetSalesByCategory(DateTime fromLocalDate, DateTime toLocalDate);

        /// <summary>
        /// One entry per local day of the period, in order; a day without any sale carries 0.
        /// </summary>
        IReadOnlyList<DailySales> GetDailySales(DateTime fromLocalDate, DateTime toLocalDate);
    }
}
