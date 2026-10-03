using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Stokbox.App.Services;
using Stokbox.App.ViewModels;
using Stokbox.App.Views;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;
using Stokbox.Core.Services;
using Xunit;

namespace Stokbox.App.Tests
{
    /// <summary>
    /// Loads the XAML of the sale flow with the application resources, as the running application does:
    /// a missing style or a broken template only fails at that moment.
    /// </summary>
    public class SaleWindowsLoadingTests
    {
        [Fact]
        public void The_sale_screen_and_its_windows_load_with_a_filled_cart()
        {
            Sta.Run(() =>
            {
                // One Application per process: this is the only test that creates it.
                var application = new App();
                application.InitializeComponent();

                var products = new StubProducts();
                var saleService = new SaleService(products, new StubSales());
                var productService = new ProductService(products, new StubCategories(), new BarcodeGenerator(new StubSequence()));
                var screen = new SaleViewModel(saleService, new StubReceipts(), new StubDialogs(), new ProductSearchViewModel(productService));
                screen.Search.Text = "2000000000015";
                screen.Search.Submit();
                screen.Search.Text = "2000000000022";
                screen.Search.Submit();

                var view = new SaleView { DataContext = screen };
                Layout(view, 1000, 600);

                // The search field and one quantity box per line of the cart.
                Assert.Equal(3, Descendants<TextBox>(view).Count());
                Assert.Contains(Descendants<TextBlock>(view), text => text.Text == "555,50 DA");

                var cart = new Cart();
                saleService.AddProduct(cart, 1);
                var payment = new PaymentWindow(new PaymentViewModel(saleService, cart));
                Layout((FrameworkElement)payment.Content, 440, 400);
                Assert.Contains(Descendants<TextBox>(payment).Select(box => box.Text), text => text == "520,50");
                payment.Close();

                var ask = new AskWindow("Monnaie à rendre : 479,50 DA", "Imprimer le ticket ?");
                Layout((FrameworkElement)ask.Content, 420, 200);
                Assert.Contains(Descendants<TextBlock>(ask), text => text.Text == "Imprimer le ticket ?");
                ask.Close();

                var history = new HistoryViewModel(saleService, new StubReceipts(), new StubDialogs());
                history.Refresh();
                history.SelectedSale = history.Sales[0];
                var historyView = new HistoryView { DataContext = history };
                Layout(historyView, 1100, 600);
                Assert.Equal(2, Descendants<DatePicker>(historyView).Count());
                Assert.Contains(Descendants<TextBlock>(historyView), text => text.Text == "Annulée");
                Assert.Contains(Descendants<TextBlock>(historyView), text => text.Text == "Retours : 520,50 DA remboursés");

                var dashboard = new DashboardViewModel(new StubDashboard(), null, () => new System.DateTime(2026, 10, 3));
                dashboard.SelectedPeriod = dashboard.Periods[1];
                dashboard.RefreshAsync().GetAwaiter().GetResult();
                var dashboardView = new DashboardView { DataContext = dashboard };
                Layout(dashboardView, 900, 1400);
                Assert.Contains(Descendants<TextBlock>(dashboardView), text => text.Text == "Produits en rupture");
                Assert.Contains(Descendants<TextBlock>(dashboardView), text => text.Text == "4 600,00 DA");
                Assert.Contains(Descendants<TextBlock>(dashboardView), text => text.Text == "23,4 %");
                Assert.Contains(Descendants<TextBlock>(dashboardView), text => text.Text == "Sans catégorie");

                // One column per day; the bar of the best day takes the whole height of the plot area.
                var dayColumns = Descendants<Grid>(dashboardView).Where(grid => grid.Name == "DayColumn").ToList();
                Assert.Equal(7, dayColumns.Count);
                Assert.Equal("03/10/2026 — 1 631,50 DA", dayColumns[6].ToolTip);
                var bars = Descendants<System.Windows.Shapes.Rectangle>(dashboardView).Where(bar => bar.Name == "PositiveBar").ToList();
                Assert.True(bars[6].ActualHeight > 100);
                Assert.Equal(bars[6].ActualHeight / 2, bars[5].ActualHeight, 0);
                Assert.Equal(0, bars[0].ActualHeight);

                var returnWindow = new ReturnWindow(new ReturnViewModel(saleService, saleService.GetSale(1)));
                Layout((FrameworkElement)returnWindow.Content, 760, 420);
                Assert.Equal(2, Descendants<TextBox>(returnWindow).Count());
                returnWindow.Close();

                var auth = new AuthService(new StubSettings(), new ReceiptSettingsService(new StubSettings()));
                var settings = new SettingsView
                {
                    DataContext = new SettingsViewModel(
                        new ShopSettingsViewModel(new ReceiptSettingsService(new StubSettings())),
                        new LabelSettingsViewModel(new LabelSettingsService(new StubSettings()), null, new StubPrinters()),
                        new ReceiptSettingsViewModel(new ReceiptSettingsService(new StubSettings()), new StubReceipts(), new StubPrinters()),
                        new SecurityViewModel(auth),
                        new BackupViewModel(new StubBackups(), null, new StubDialogs(), null),
                        new AboutViewModel(new Stokbox.Core.AppPaths("C:/ProgramData/Stokbox")))
                };
                ((SettingsViewModel)settings.DataContext).Load();
                Layout(settings, 1000, 600);
                var tabs = Descendants<TabControl>(settings).Single();
                Assert.Equal(
                    new[] { "Boutique", "Étiquettes", "Ticket", "Sécurité", "Sauvegarde", "À propos" },
                    tabs.Items.Cast<TabItem>().Select(tab => (string)tab.Header));

                // Each tab builds its content when it is shown.
                for (var i = 0; i < tabs.Items.Count; i++)
                {
                    tabs.SelectedIndex = i;
                    Layout(settings, 1000, 600);
                }

                tabs.SelectedIndex = 3;
                Layout(settings, 1000, 600);
                Assert.Equal(3, Descendants<PasswordBox>(settings).Count());

                var login = new LoginWindow(new LoginViewModel(auth));
                Layout((FrameworkElement)login.Content, 400, 300);
                Assert.Single(Descendants<PasswordBox>(login));
                login.Close();

                var setup = new SetupWindow(new SetupViewModel(auth));
                Layout((FrameworkElement)setup.Content, 480, 600);
                Assert.Equal(2, Descendants<PasswordBox>(setup).Count());
                Assert.Equal(3, Descendants<TextBox>(setup).Count());
                setup.Close();
            });
        }

        private static void Layout(FrameworkElement element, double width, double height)
        {
            element.Measure(new Size(width, height));
            element.Arrange(new Rect(0, 0, width, height));
            element.UpdateLayout();
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is Window window && window.Content is DependencyObject content)
            {
                root = content;
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var descendant in Descendants<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private sealed class StubProducts : IProductRepository
        {
            private readonly List<Product> _products = new List<Product>
            {
                new Product { Id = 1, Barcode = "2000000000015", Name = "Café moulu", PurchasePriceCents = 40000, SalePriceCents = 52050, StockQuantity = 10 },
                new Product { Id = 2, Barcode = "2000000000022", Name = "Eau minérale", PurchasePriceCents = 2500, SalePriceCents = 3500, StockQuantity = 10 }
            };

            public Product GetById(long id) => _products.FirstOrDefault(p => p.Id == id);

            public Product GetByBarcode(string barcode) => _products.FirstOrDefault(p => p.Barcode == barcode);

            public IReadOnlyList<Product> Search(ProductSearchCriteria criteria) => _products;

            public long Insert(string barcode, string name, long categoryId, long purchasePriceCents, long salePriceCents, System.DateTime createdAtUtc) => 0;

            public bool Update(long id, string name, long categoryId, long purchasePriceCents, long salePriceCents) => false;

            public bool SetArchived(long id, bool isArchived) => false;
        }

        private sealed class StubSales : ISaleRepository
        {
            public Sale Create(NewSale sale) => new Sale { Number = "V-20261003-0001", Lines = sale.Lines };

            public IReadOnlyList<SaleSummary> Search(System.DateTime fromUtc, System.DateTime toUtc, string numberText) => new[]
            {
                new SaleSummary { Id = 1, Number = "V-20261003-0001", CreatedAtUtc = System.DateTime.UtcNow, ArticleCount = 3, TotalCents = 107600, Status = Sale.StatusValidated },
                new SaleSummary { Id = 2, Number = "V-20261003-0002", CreatedAtUtc = System.DateTime.UtcNow, ArticleCount = 1, TotalCents = 3500, Status = Sale.StatusCancelled }
            };

            public Sale GetById(long saleId) => new Sale
            {
                Id = saleId,
                Number = "V-20261003-0001",
                CreatedAtUtc = System.DateTime.UtcNow,
                TotalCents = 107600,
                ReceivedCents = 110000,
                ChangeCents = 2400,
                Status = Sale.StatusValidated,
                Lines = new[]
                {
                    new SaleLine { Id = 1, ProductName = "Café moulu", Quantity = 2, UnitPriceCents = 52050, LineTotalCents = 104100, ReturnedQuantity = 1 },
                    new SaleLine { Id = 2, ProductName = "Eau minérale", Quantity = 1, UnitPriceCents = 3500, LineTotalCents = 3500 }
                }
            };

            public IReadOnlyList<SaleReturn> GetReturns(long saleId) => new[]
            {
                new SaleReturn
                {
                    Id = 1,
                    SaleId = saleId,
                    CreatedAtUtc = System.DateTime.UtcNow,
                    Lines = new[] { new SaleReturnLine { SaleLineId = 1, ProductName = "Café moulu", Quantity = 1, UnitPriceCents = 52050 } }
                }
            };

            public void Cancel(long saleId, System.DateTime cancelledAtUtc)
            {
            }

            public SaleReturn Return(long saleId, IReadOnlyList<ReturnRequestLine> lines, System.DateTime returnedAtUtc) => null;
        }

        private sealed class StubDashboard : IDashboardService
        {
            public Stokbox.Core.Dashboard.StockSummary GetStockSummary() =>
                new Stokbox.Core.Dashboard.StockSummary(460000, 604500, 2, 34, 1);

            public Stokbox.Core.Dashboard.SalesKpis GetSalesKpis(System.DateTime fromLocalDate, System.DateTime toLocalDate) =>
                new Stokbox.Core.Dashboard.SalesKpis(1, 267250, 163150, 125000, 5);

            public IReadOnlyList<Stokbox.Core.Dashboard.TopProduct> GetTopProducts(System.DateTime fromLocalDate, System.DateTime toLocalDate, int limit = 10) => new[]
            {
                new Stokbox.Core.Dashboard.TopProduct("Café moulu", 3, 156150),
                new Stokbox.Core.Dashboard.TopProduct("Eau minérale", 2, 7000)
            };

            public IReadOnlyList<Stokbox.Core.Dashboard.CategorySales> GetSalesByCategory(System.DateTime fromLocalDate, System.DateTime toLocalDate) => new[]
            {
                new Stokbox.Core.Dashboard.CategorySales("Épicerie", 156150),
                new Stokbox.Core.Dashboard.CategorySales(null, 7000)
            };

            public IReadOnlyList<Stokbox.Core.Dashboard.DailySales> GetDailySales(System.DateTime fromLocalDate, System.DateTime toLocalDate) =>
                Enumerable.Range(0, 7)
                    .Select(i => new Stokbox.Core.Dashboard.DailySales(
                        fromLocalDate.AddDays(i),
                        i == 6 ? 163150 : i == 5 ? 81575 : 0))
                    .ToList();
        }

        private sealed class StubCategories : ICategoryRepository
        {
            public IReadOnlyList<Category> GetAll() => new Category[0];

            public Category GetById(long id) => null;

            public long Insert(string name) => 0;

            public bool Rename(long id, string name) => false;

            public int CountProducts(long id) => 0;

            public bool Delete(long id) => false;
        }

        private sealed class StubSequence : IBarcodeSequence
        {
            public long Next() => 1;
        }

        private sealed class StubSettings : ISettingsRepository
        {
            public IReadOnlyDictionary<string, string> GetAll() => new Dictionary<string, string>();

            public void Save(IReadOnlyDictionary<string, string> values)
            {
            }
        }

        private sealed class StubBackups : IBackupService
        {
            public string BackupsDirectory => "C:/ProgramData/Stokbox/backups";

            public string BackupTo(string directory) => null;

            public string BackupAutomatically() => null;

            public void CheckRestorable(string filePath)
            {
            }

            public string Restore(string filePath) => null;
        }

        private sealed class StubPrinters : IPrinterCatalog
        {
            public IReadOnlyList<string> GetPrinterNames() => new[] { "Ticket 80 mm" };
        }

        private sealed class StubReceipts : IReceiptPrintService
        {
            public bool IsEnabled => false;

            public bool Print(Sale sale) => false;

            public bool PrintTest(ReceiptSettings settings) => false;
        }

        private sealed class StubDialogs : IDialogService
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

            public bool Ask(string headline, string question) => false;

            public bool ShowReturn(ReturnViewModel viewModel) => false;
        }
    }
}
