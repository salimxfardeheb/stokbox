using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Dapper;
using Stokbox.App.Services;
using Stokbox.App.ViewModels;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;
using Stokbox.Data;
using Stokbox.Data.Migrations;
using Stokbox.Data.Repositories;
using Xunit;

namespace Stokbox.App.Tests
{
    /// <summary>
    /// The sale screen driven as the keyboard and the scanner drive it, on a real temporary database.
    /// </summary>
    public class SaleScreenTests : IDisposable
    {
        private const string Coffee = "2000000000015";
        private const string Water = "2000000000022";
        private const string Tea = "2000000000039";

        private readonly string _directory = Path.Combine(Path.GetTempPath(), "stokbox-tests", Guid.NewGuid().ToString("N"));
        private readonly SqliteConnectionFactory _connectionFactory;
        private readonly ProductRepository _products;
        private readonly FakeDialogs _dialogs = new FakeDialogs();
        private readonly FakeReceipts _receipts = new FakeReceipts();
        private readonly SaleViewModel _screen;

        public SaleScreenTests()
        {
            _connectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "stokbox.db"));
            new MigrationRunner(_connectionFactory).MigrateToLatest();

            _products = new ProductRepository(_connectionFactory);
            var movements = new StockMovementRepository(_connectionFactory);
            var categoryId = new CategoryRepository(_connectionFactory).Insert("Épicerie");
            movements.AddEntry(_products.Insert(Coffee, "Café moulu", categoryId, 40000, 52050, DateTime.UtcNow), 10, DateTime.UtcNow);
            movements.AddEntry(_products.Insert(Water, "Eau minérale", categoryId, 2500, 3500, DateTime.UtcNow), 2, DateTime.UtcNow);
            movements.AddEntry(_products.Insert(Tea, "Thé vert", categoryId, 9000, 12000, DateTime.UtcNow), 5, DateTime.UtcNow);

            var productService = new ProductService(
                _products,
                new CategoryRepository(_connectionFactory),
                new BarcodeGenerator(new SqliteBarcodeSequence(_connectionFactory)));
            var saleService = new SaleService(_products, new SaleRepository(_connectionFactory));

            _screen = new SaleViewModel(saleService, _receipts, _dialogs, new ProductSearchViewModel(productService));
            _screen.Activate();
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
        public void Three_scans_then_F2_and_Enter_record_the_sale_and_empty_the_cart()
        {
            Scan(Coffee);
            Scan(Water);
            Scan(Tea);

            Assert.Equal(new[] { "Café moulu", "Eau minérale", "Thé vert" }, _screen.Lines.Select(l => l.Name));
            Assert.Equal("675,50 DA", _screen.TotalText);
            Assert.Equal("3 articles", _screen.ArticleCountText);

            // F2 opens the payment, pre-filled with the total; Enter validates it.
            _dialogs.OnPayment = payment =>
            {
                Assert.Equal("675,50 DA", payment.TotalText);
                Assert.Equal("675,50", payment.ReceivedText);
                Assert.Equal("0,00 DA", payment.ChangeText);
                payment.ConfirmCommand.Execute(null);
            };
            _screen.CheckoutCommand.Execute(null);

            Assert.Empty(_screen.Lines);
            Assert.Equal("0,00 DA", _screen.TotalText);
            Assert.Null(_screen.Message);
            Assert.StartsWith("Vente V-", _screen.LastSaleText);
            Assert.Contains("monnaie rendue : 0,00 DA", _screen.LastSaleText);
            Assert.Equal(1, Count("sales"));
            Assert.Equal(3, Count("sale_lines"));
            Assert.Equal(9, _products.GetByBarcode(Coffee).StockQuantity);
            Assert.Equal(1, _products.GetByBarcode(Water).StockQuantity);
            Assert.Equal(4, _products.GetByBarcode(Tea).StockQuantity);
        }

        [Fact]
        public void Scanning_the_same_article_twice_adds_to_its_line_and_selects_it()
        {
            Scan(Coffee);
            Scan(Water);
            Scan(Coffee);

            Assert.Equal(new[] { 2, 1 }, _screen.Lines.Select(l => l.Quantity));
            Assert.Equal("Café moulu", _screen.SelectedLine.Name);
            Assert.Equal("1 076,00 DA", _screen.TotalText);
        }

        [Fact]
        public void A_scan_beyond_the_stock_is_refused_with_the_quantity_available()
        {
            Scan(Water);
            Scan(Water);
            Scan(Water);

            Assert.Equal("Stock insuffisant : 2 disponible(s)", _screen.Message);
            Assert.Equal(2, _screen.Lines.Single().Quantity);
        }

        [Fact]
        public void An_unknown_barcode_leaves_the_cart_untouched()
        {
            Scan("2000000009999");

            Assert.Empty(_screen.Lines);
            Assert.Equal("Aucun produit avec le code-barres 2000000009999.", _screen.Search.Message);
        }

        [Fact]
        public void Plus_minus_and_delete_act_on_the_selected_line()
        {
            Scan(Coffee);
            Scan(Water);

            _screen.MoveSelection(-1);
            Assert.Equal("Café moulu", _screen.SelectedLine.Name);

            _screen.IncreaseCommand.Execute(null);
            _screen.IncreaseCommand.Execute(null);
            Assert.Equal(3, _screen.SelectedLine.Quantity);
            Assert.Equal("1 596,50 DA", _screen.TotalText);

            _screen.DecreaseCommand.Execute(null);
            Assert.Equal(2, _screen.SelectedLine.Quantity);

            // The quantity never goes below 1 with "-": Delete removes the line.
            _screen.DecreaseCommand.Execute(null);
            _screen.DecreaseCommand.Execute(null);
            Assert.Equal(1, _screen.SelectedLine.Quantity);

            _screen.RemoveSelectedCommand.Execute(null);
            Assert.Equal("Eau minérale", _screen.Lines.Single().Name);
            Assert.Equal("Eau minérale", _screen.SelectedLine.Name);
        }

        [Fact]
        public void A_quantity_typed_in_the_grid_is_applied_or_refused()
        {
            Scan(Water);
            var line = _screen.Lines.Single();

            line.QuantityText = "2";
            Assert.Equal(2, line.Quantity);
            Assert.Null(_screen.Message);

            line.QuantityText = "3";
            Assert.Equal("Stock insuffisant : 2 disponible(s)", _screen.Message);
            Assert.Equal("2", line.QuantityText);

            line.QuantityText = "abc";
            Assert.Equal(2, line.Quantity);
            Assert.NotNull(_screen.Message);
        }

        [Fact]
        public void Escape_empties_the_cart_only_after_confirmation()
        {
            Scan(Coffee);

            _dialogs.ConfirmAnswer = false;
            _screen.ClearCartCommand.Execute(null);
            Assert.Single(_screen.Lines);

            _dialogs.ConfirmAnswer = true;
            _screen.ClearCartCommand.Execute(null);
            Assert.Empty(_screen.Lines);
            Assert.Equal(0, Count("sales"));
        }

        [Fact]
        public void The_change_follows_the_amount_typed_and_too_little_cash_is_refused()
        {
            Scan(Coffee);

            _dialogs.OnPayment = payment =>
            {
                payment.ReceivedText = "500";
                Assert.True(payment.IsShort);
                Assert.Equal("Il manque", payment.ChangeCaption);
                Assert.Equal("20,50 DA", payment.ChangeText);

                payment.ConfirmCommand.Execute(null);
                Assert.Null(payment.Sale);
                Assert.Contains("Montant reçu insuffisant", payment.Error);

                payment.ReceivedText = "abc";
                payment.ConfirmCommand.Execute(null);
                Assert.Null(payment.Sale);

                payment.ReceivedText = "1000";
                Assert.False(payment.IsShort);
                Assert.Equal("Monnaie à rendre", payment.ChangeCaption);
                Assert.Equal("479,50 DA", payment.ChangeText);

                // Escape: the payment is abandoned.
            };
            _screen.CheckoutCommand.Execute(null);

            Assert.Single(_screen.Lines);
            Assert.Equal(0, Count("sales"));
            Assert.Null(_screen.LastSaleText);
        }

        [Fact]
        public void The_receipt_is_offered_after_the_sale_and_printed_on_Enter()
        {
            _receipts.IsEnabled = true;
            _dialogs.AskAnswer = true;
            Scan(Coffee);
            _dialogs.OnPayment = payment =>
            {
                payment.ReceivedText = "1000";
                payment.ConfirmCommand.Execute(null);
            };

            _screen.CheckoutCommand.Execute(null);

            Assert.Equal("Monnaie à rendre : 479,50 DA", _dialogs.Asked.Single());
            var sale = _receipts.Printed.Single();
            Assert.Equal(52050, sale.TotalCents);
            Assert.Equal(100000, sale.ReceivedCents);
            Assert.Equal(47950, sale.ChangeCents);
            Assert.Equal("Café moulu", sale.Lines.Single().ProductName);
            Assert.Empty(_screen.Lines);
        }

        [Fact]
        public void The_receipt_is_not_printed_on_Escape()
        {
            _receipts.IsEnabled = true;
            _dialogs.AskAnswer = false;
            Scan(Coffee);
            _dialogs.OnPayment = payment => payment.ConfirmCommand.Execute(null);

            _screen.CheckoutCommand.Execute(null);

            Assert.Single(_dialogs.Asked);
            Assert.Empty(_receipts.Printed);
            Assert.Equal(1, Count("sales"));
        }

        [Fact]
        public void Without_a_receipt_printer_the_receipt_is_never_offered()
        {
            _receipts.IsEnabled = false;
            Scan(Coffee);
            _dialogs.OnPayment = payment => payment.ConfirmCommand.Execute(null);

            _screen.CheckoutCommand.Execute(null);

            Assert.Empty(_dialogs.Asked);
            Assert.Empty(_receipts.Printed);
            Assert.Equal(1, Count("sales"));
        }

        [Fact]
        public void Checking_out_an_empty_cart_does_nothing()
        {
            _screen.CheckoutCommand.Execute(null);

            Assert.Equal("Le panier est vide.", _screen.Message);
            Assert.Equal(0, _dialogs.PaymentsShown);
        }

        [Fact]
        public void After_a_sale_the_search_field_is_asked_to_take_the_focus_again()
        {
            var focusRequests = 0;
            _screen.Search.FocusRequested += (sender, e) => focusRequests++;
            Scan(Coffee);
            _dialogs.OnPayment = payment => payment.ConfirmCommand.Execute(null);
            focusRequests = 0;

            _screen.CheckoutCommand.Execute(null);

            Assert.True(focusRequests >= 1);
            Assert.Equal(string.Empty, _screen.Search.Text);
        }

        // What the scanner does: the digits, then Enter.
        private void Scan(string barcode)
        {
            _screen.Search.Text = barcode;
            _screen.Search.Submit();
        }

        private int Count(string table)
        {
            using (var connection = _connectionFactory.Open())
            {
                // The table name comes from the test itself, never from user input.
                return connection.ExecuteScalar<int>("SELECT COUNT(*) FROM " + table);
            }
        }

        private sealed class FakeDialogs : IDialogService
        {
            public Action<PaymentViewModel> OnPayment { get; set; }

            public bool ConfirmAnswer { get; set; }

            public bool AskAnswer { get; set; }

            public int PaymentsShown { get; private set; }

            public List<string> Asked { get; } = new List<string>();

            public bool ShowPayment(PaymentViewModel viewModel)
            {
                PaymentsShown++;
                OnPayment?.Invoke(viewModel);
                return viewModel.Sale != null;
            }

            public bool Ask(string headline, string question)
            {
                Asked.Add(headline);
                return AskAnswer;
            }

            public bool ShowReturn(ReturnViewModel viewModel)
            {
                return false;
            }

            public bool Confirm(string message)
            {
                return ConfirmAnswer;
            }

            public void ShowWarning(string message)
            {
            }

            public bool ShowProductForm(ProductFormViewModel viewModel)
            {
                return false;
            }

            public void ShowCategories(CategoriesViewModel viewModel)
            {
            }
        }

        private sealed class FakeReceipts : IReceiptPrintService
        {
            public bool IsEnabled { get; set; }

            public List<Sale> Printed { get; } = new List<Sale>();

            public bool Print(Sale sale)
            {
                Printed.Add(sale);
                return true;
            }

            public bool PrintTest(ReceiptSettings settings)
            {
                return true;
            }
        }
    }
}
