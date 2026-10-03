using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Stokbox.App.Services;
using Stokbox.App.ViewModels;
using Stokbox.Core.Dashboard;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;
using Stokbox.Data;
using Stokbox.Data.Migrations;
using Stokbox.Data.Repositories;
using Xunit;

namespace Stokbox.App.Tests
{
    /// <summary>
    /// The dashboard screen on a real temporary database: tiles, periods, chart, tables, out-of-stock link.
    /// </summary>
    public class DashboardScreenTests : IDisposable
    {
        // "Today" for the screen; the sales are made at noon, local time.
        private static readonly DateTime Today = new DateTime(2026, 10, 3);

        private readonly string _directory = Path.Combine(Path.GetTempPath(), "stokbox-tests", Guid.NewGuid().ToString("N"));
        private readonly SqliteConnectionFactory _connectionFactory;
        private readonly ProductRepository _products;
        private readonly SaleService _saleService;
        private readonly DashboardViewModel _screen;
        private readonly long _coffeeId;
        private readonly long _waterId;

        private DateTime _now = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Local).ToUniversalTime();

        public DashboardScreenTests()
        {
            _connectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "stokbox.db"));
            new MigrationRunner(_connectionFactory).MigrateToLatest();

            _products = new ProductRepository(_connectionFactory);
            var movements = new StockMovementRepository(_connectionFactory);
            var categoryId = new CategoryRepository(_connectionFactory).Insert("Épicerie");
            _coffeeId = _products.Insert("2000000000015", "Café moulu", categoryId, 40000, 52050, _now);
            _waterId = _products.Insert("2000000000022", "Eau minérale", categoryId, 2500, 3500, _now);
            _products.Insert("2000000000039", "Sucre", categoryId, 5000, 7000, _now);
            movements.AddEntry(_coffeeId, 10, _now);
            movements.AddEntry(_waterId, 24, _now);

            _saleService = new SaleService(_products, new SaleRepository(_connectionFactory), () => _now);
            _screen = new DashboardViewModel(new SqliteDashboardService(_connectionFactory), null, () => Today);
        }

        public void Dispose()
        {
            try
            {
                SQLiteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        [Fact]
        public async Task Without_any_sale_the_stock_is_valued_and_the_ratios_have_no_value()
        {
            Assert.Equal("Aujourd'hui", _screen.SelectedPeriod.Name);
            Assert.Equal("—", _screen.StockPurchaseValueText);

            await _screen.RefreshAsync();

            Assert.False(_screen.IsLoading);
            Assert.Null(_screen.ErrorMessage);

            // 10 x 400,00 + 24 x 25,00, then 10 x 520,50 + 24 x 35,00
            Assert.Equal("4 600,00 DA", _screen.StockPurchaseValueText);
            Assert.Equal("6 045,00 DA", _screen.StockSaleValueText);
            Assert.Equal("1 445,00 DA", _screen.StockMarginText);
            Assert.Equal("2", _screen.StockReferenceCountText);
            Assert.Equal("34", _screen.StockUnitCountText);
            Assert.Equal("1", _screen.OutOfStockCountText);

            Assert.Equal("0,00 DA", _screen.NetRevenueText);
            Assert.Equal("0,00 DA", _screen.GrossProfitText);
            Assert.Equal("—", _screen.MarginRateText);
            Assert.Equal("0", _screen.SaleCountText);
            Assert.Equal("—", _screen.AverageBasketText);
            Assert.Equal("0", _screen.NetItemCountText);

            Assert.Equal("Le 03/10/2026", _screen.PeriodText);
            Assert.Equal("03/10/2026 — 0,00 DA", _screen.DailyBars.Single().ToolTipText);
            Assert.True(_screen.HasNoTopProducts);
            Assert.True(_screen.HasNoCategories);
        }

        [Fact]
        public async Task A_sale_and_a_partial_return_show_in_the_tiles_the_chart_and_the_tables()
        {
            var sale = Sell(coffee: 5, water: 2);
            var coffeeLine = _saleService.GetSale(sale.Id).Lines.Single(line => line.ProductId == _coffeeId).Id;
            _saleService.ReturnItems(sale.Id, new[] { new ReturnRequestLine(coffeeLine, 2) });

            await _screen.RefreshAsync();

            // 3 x 520,50 + 2 x 35,00 ; cost 3 x 400,00 + 2 x 25,00 ; 381,50 / 1 631,50 = 23,38 %
            Assert.Equal("1 631,50 DA", _screen.NetRevenueText);
            Assert.Equal("381,50 DA", _screen.GrossProfitText);
            Assert.Equal("23,4 %", _screen.MarginRateText);
            Assert.Equal("1", _screen.SaleCountText);
            Assert.Equal("2 672,50 DA", _screen.AverageBasketText);
            Assert.Equal("5", _screen.NetItemCountText);

            // 7 coffees and 22 waters are left.
            Assert.Equal("29", _screen.StockUnitCountText);
            Assert.Equal("3 350,00 DA", _screen.StockPurchaseValueText);

            var bar = _screen.DailyBars.Single();
            Assert.Equal("03/10/2026 — 1 631,50 DA", bar.ToolTipText);
            Assert.Equal("03/10", bar.Label);
            Assert.Equal(1, bar.Positive.Value);
            Assert.Equal(0, bar.TopGap.Value);
            Assert.Equal("1 631,50 DA", _screen.ChartMaxText);
            Assert.Null(_screen.ChartMinText);

            Assert.Equal(new[] { "Café moulu", "Eau minérale" }, _screen.TopProducts.Select(p => p.Name));
            Assert.Equal(new[] { "3", "2" }, _screen.TopProducts.Select(p => p.QuantityText));
            Assert.Equal(new[] { "1 561,50 DA", "70,00 DA" }, _screen.TopProducts.Select(p => p.RevenueText));

            var category = _screen.Categories.Single();
            Assert.Equal("Épicerie", category.Name);
            Assert.Equal("1 631,50 DA", category.RevenueText);
            Assert.Equal("100,0 %", category.ShareText);
            Assert.False(_screen.HasNoTopProducts);
        }

        [Fact]
        public async Task Each_period_covers_its_whole_local_days()
        {
            Sell(coffee: 1, water: 0);
            _now = new DateTime(2026, 9, 30, 23, 30, 0, DateTimeKind.Local).ToUniversalTime();
            Sell(coffee: 0, water: 2);

            await Select(DashboardPeriod.Last7Days);
            Assert.Equal("Du 27/09/2026 au 03/10/2026", _screen.PeriodText);
            Assert.Equal(7, _screen.DailyBars.Count);
            Assert.Equal("27/09", _screen.DailyBars[0].Label);
            Assert.Equal("30/09/2026 — 70,00 DA", _screen.DailyBars[3].ToolTipText);
            Assert.Equal("590,50 DA", _screen.NetRevenueText);
            Assert.Equal("2", _screen.SaleCountText);

            // The bars share one scale: the best day fills the height, the others are in proportion.
            Assert.Equal(1, _screen.DailyBars[6].Positive.Value, 6);
            Assert.Equal(7000d / 52050d, _screen.DailyBars[3].Positive.Value, 6);
            Assert.Equal(0, _screen.DailyBars[0].Positive.Value);

            await Select(DashboardPeriod.CurrentMonth);
            Assert.Equal("Du 01/10/2026 au 03/10/2026", _screen.PeriodText);
            Assert.Equal(3, _screen.DailyBars.Count);
            Assert.Equal("520,50 DA", _screen.NetRevenueText);

            await Select(DashboardPeriod.PreviousMonth);
            Assert.Equal("Du 01/09/2026 au 30/09/2026", _screen.PeriodText);
            Assert.Equal(30, _screen.DailyBars.Count);
            Assert.Equal("30", _screen.DailyBars[29].Label);
            Assert.Equal("70,00 DA", _screen.NetRevenueText);

            await Select(DashboardPeriod.Today);
            Assert.Equal("520,50 DA", _screen.NetRevenueText);
            Assert.False(_screen.IsCustomPeriod);
        }

        [Fact]
        public async Task A_custom_period_uses_its_two_dates_in_any_order()
        {
            Sell(coffee: 1, water: 0);

            await Select(DashboardPeriod.Custom);
            Assert.True(_screen.IsCustomPeriod);
            Assert.Equal("Le 03/10/2026", _screen.PeriodText);

            _screen.CustomFrom = new DateTime(2026, 10, 2);
            _screen.CustomTo = new DateTime(2026, 8, 24);
            await _screen.RefreshAsync();

            Assert.Equal("Du 24/08/2026 au 02/10/2026", _screen.PeriodText);
            Assert.Equal("0,00 DA", _screen.NetRevenueText);

            // 40 days: too many for one label each, the ends of the period are named instead.
            Assert.Equal(40, _screen.DailyBars.Count);
            Assert.All(_screen.DailyBars, bar => Assert.Equal(string.Empty, bar.Label));
            Assert.Equal("24/08/2026", _screen.ChartFirstDayText);
            Assert.Equal("02/10/2026", _screen.ChartLastDayText);

            _screen.CustomTo = null;
            await _screen.RefreshAsync();
            Assert.Equal("Du 02/10/2026 au 03/10/2026", _screen.PeriodText);
            Assert.Equal("520,50 DA", _screen.NetRevenueText);
        }

        [Fact]
        public async Task A_day_with_more_returns_than_sales_is_drawn_under_the_zero_line()
        {
            var sale = Sell(coffee: 4, water: 0);
            var coffeeLine = _saleService.GetSale(sale.Id).Lines.Single().Id;
            _now = _now.AddDays(1);
            _saleService.ReturnItems(sale.Id, new[] { new ReturnRequestLine(coffeeLine, 1) });

            _screen.CustomFrom = new DateTime(2026, 10, 3);
            _screen.CustomTo = new DateTime(2026, 10, 4);
            await Select(DashboardPeriod.Custom);

            // 2 082,00 DA sold on the 3rd, 520,50 DA returned on the 4th: a fifth of the height is under zero.
            Assert.Equal("2 082,00 DA", _screen.ChartMaxText);
            Assert.Equal("-520,50 DA", _screen.ChartMinText);
            Assert.Equal(0.8, _screen.DailyBars[0].Positive.Value, 6);
            Assert.Equal(0.2, _screen.DailyBars[0].BottomGap.Value, 6);
            Assert.Equal(0.8, _screen.DailyBars[1].TopGap.Value, 6);
            Assert.Equal(0.2, _screen.DailyBars[1].Negative.Value, 6);
            Assert.Equal("04/10/2026 — -520,50 DA", _screen.DailyBars[1].ToolTipText);
        }

        [Fact]
        public void The_out_of_stock_tile_asks_for_the_products_screen_filtered_on_them()
        {
            var requests = 0;
            _screen.OutOfStockRequested += (sender, e) => requests++;

            _screen.ShowOutOfStockCommand.Execute(null);

            Assert.Equal(1, requests);

            // What the main window then does: filter, then show the products screen.
            var categories = new CategoryRepository(_connectionFactory);
            var products = new ProductsViewModel(
                new ProductService(_products, categories, new BarcodeGenerator(new SqliteBarcodeSequence(_connectionFactory))),
                new CategoryService(categories),
                new NoDialogs());
            products.SearchText = "café";
            products.Refresh();
            Assert.Equal(new[] { "Café moulu" }, products.Products.Select(p => p.Name));

            products.FilterOutOfStock();
            products.Refresh();

            Assert.True(products.ShowOutOfStockOnly);
            Assert.Equal(string.Empty, products.SearchText);
            Assert.Equal("Toutes les catégories", products.SelectedCategoryFilter.Name);
            Assert.Equal(new[] { "Sucre" }, products.Products.Select(p => p.Name));

            products.ShowOutOfStockOnly = false;
            Assert.Equal(3, products.Products.Count);
        }

        [Fact]
        public async Task A_failed_computation_is_reported_and_logged_and_the_screen_stays_usable()
        {
            var log = new RecordingLog();
            var screen = new DashboardViewModel(new FailingDashboard(), log, () => Today);

            await screen.RefreshAsync();

            Assert.False(screen.IsLoading);
            Assert.Contains("n'ont pas pu être calculés", screen.ErrorMessage);
            Assert.Equal("Tableau de bord", log.Contexts.Single());
            Assert.Equal("—", screen.NetRevenueText);
            Assert.True(screen.RefreshCommand.CanExecute(null));
        }

        private async Task Select(DashboardPeriod period)
        {
            // Choosing a period starts a computation; this one, started last, is the one displayed.
            _screen.SelectedPeriod = _screen.Periods.Single(option => option.Period == period);
            await _screen.RefreshAsync();
        }

        private Sale Sell(int coffee, int water)
        {
            var cart = new Cart();
            if (coffee > 0)
            {
                _saleService.AddProduct(cart, _coffeeId);
                _saleService.SetQuantity(cart, _coffeeId, coffee);
            }

            if (water > 0)
            {
                _saleService.AddProduct(cart, _waterId);
                _saleService.SetQuantity(cart, _waterId, water);
            }

            return _saleService.Validate(cart, 10000000);
        }

        private sealed class FailingDashboard : IDashboardService
        {
            public StockSummary GetStockSummary() => throw new InvalidOperationException("panne simulée");

            public SalesKpis GetSalesKpis(DateTime fromLocalDate, DateTime toLocalDate) => null;

            public IReadOnlyList<TopProduct> GetTopProducts(DateTime fromLocalDate, DateTime toLocalDate, int limit = 10) => null;

            public IReadOnlyList<CategorySales> GetSalesByCategory(DateTime fromLocalDate, DateTime toLocalDate) => null;

            public IReadOnlyList<DailySales> GetDailySales(DateTime fromLocalDate, DateTime toLocalDate) => null;
        }

        private sealed class RecordingLog : IErrorLog
        {
            public List<string> Contexts { get; } = new List<string>();

            public string LogsDirectory => "C:/ProgramData/Stokbox/logs";

            public void Write(string context, Exception exception)
            {
                Contexts.Add(context);
            }
        }

        private sealed class NoDialogs : IDialogService
        {
            public bool ShowProductForm(ProductFormViewModel viewModel) => false;

            public void ShowCategories(CategoriesViewModel viewModel)
            {
            }

            public bool Confirm(string message) => false;

            public void ShowWarning(string message)
            {
            }

            public bool ShowPayment(PaymentViewModel viewModel) => false;

            public bool ShowReturn(ReturnViewModel viewModel) => false;

            public bool Ask(string headline, string question) => false;
        }
    }
}
