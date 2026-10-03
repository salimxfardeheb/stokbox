using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
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
    /// The history screen on a real temporary database: list, detail, return, cancellation, reprint.
    /// </summary>
    public class HistoryScreenTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "stokbox-tests", Guid.NewGuid().ToString("N"));
        private readonly ProductRepository _products;
        private readonly SaleService _saleService;
        private readonly FakeDialogs _dialogs = new FakeDialogs();
        private readonly FakeReceipts _receipts = new FakeReceipts();
        private readonly HistoryViewModel _screen;
        private readonly long _coffeeId;
        private readonly long _waterId;

        public HistoryScreenTests()
        {
            var connectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "stokbox.db"));
            new MigrationRunner(connectionFactory).MigrateToLatest();

            _products = new ProductRepository(connectionFactory);
            var movements = new StockMovementRepository(connectionFactory);
            var categoryId = new CategoryRepository(connectionFactory).Insert("Épicerie");
            _coffeeId = _products.Insert("2000000000015", "Café moulu", categoryId, 40000, 52050, DateTime.UtcNow);
            _waterId = _products.Insert("2000000000022", "Eau minérale", categoryId, 2500, 3500, DateTime.UtcNow);
            movements.AddEntry(_coffeeId, 10, DateTime.UtcNow);
            movements.AddEntry(_waterId, 24, DateTime.UtcNow);

            _saleService = new SaleService(_products, new SaleRepository(connectionFactory));
            _screen = new HistoryViewModel(_saleService, _receipts, _dialogs);
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
        public void The_sales_of_today_are_listed_by_default()
        {
            var first = Sell(coffee: 1, water: 2);
            var second = Sell(coffee: 2, water: 0);

            _screen.Refresh();

            Assert.Equal(DateTime.Today, _screen.FromDate);
            Assert.Equal(DateTime.Today, _screen.ToDate);
            Assert.Equal(new[] { second.Number, first.Number }, _screen.Sales.Select(s => s.Number));
            Assert.Equal(new[] { 2, 3 }, _screen.Sales.Select(s => s.ArticleCount));
            Assert.Equal("2 ventes — dont 2 validées pour 1 631,50 DA", _screen.SummaryText);
            Assert.False(_screen.HasSelection);
        }

        [Fact]
        public void Another_period_or_a_number_changes_the_list()
        {
            var first = Sell(coffee: 1, water: 0);
            Sell(coffee: 1, water: 0);
            _screen.Refresh();

            _screen.FromDate = DateTime.Today.AddDays(-7);
            _screen.ToDate = DateTime.Today.AddDays(-1);
            Assert.Empty(_screen.Sales);
            Assert.Equal("Aucune vente.", _screen.SummaryText);

            // A number is found whatever the period shown.
            _screen.NumberText = first.Number;
            Assert.Equal(new[] { first.Number }, _screen.Sales.Select(s => s.Number));

            _screen.TodayCommand.Execute(null);
            Assert.Equal(string.Empty, _screen.NumberText);
            Assert.Equal(DateTime.Today, _screen.FromDate);
            Assert.Equal(2, _screen.Sales.Count);
        }

        [Fact]
        public void Selecting_a_sale_shows_its_lines()
        {
            var sale = Sell(coffee: 2, water: 1);
            _screen.Refresh();

            _screen.SelectedSale = _screen.Sales.Single();

            Assert.True(_screen.HasSelection);
            Assert.StartsWith("Vente " + sale.Number + " — ", _screen.DetailTitle);
            Assert.EndsWith(" — Validée", _screen.DetailTitle);
            Assert.Equal("Total 1 076,00 DA — reçu 5 000,00 DA — rendu 3 924,00 DA", _screen.PaymentText);
            Assert.Equal(new[] { "Café moulu", "Eau minérale" }, _screen.Lines.Select(l => l.ProductName));
            Assert.Equal(new[] { 2, 1 }, _screen.Lines.Select(l => l.Quantity));
            Assert.False(_screen.HasReturns);
            Assert.Empty(_screen.ReturnRows);
            Assert.True(_screen.CancelSaleCommand.CanExecute(null));
            Assert.True(_screen.ReturnItemsCommand.CanExecute(null));
        }

        [Fact]
        public void Without_a_selection_nothing_can_be_cancelled_returned_or_reprinted()
        {
            Sell(coffee: 1, water: 0);
            _screen.Refresh();

            Assert.False(_screen.CancelSaleCommand.CanExecute(null));
            Assert.False(_screen.ReturnItemsCommand.CanExecute(null));
            Assert.False(_screen.ReprintCommand.CanExecute(null));
        }

        [Fact]
        public void A_return_shows_the_returnable_quantities_and_the_refund_then_updates_the_detail()
        {
            Sell(coffee: 5, water: 2);
            SelectFirst();

            _dialogs.OnReturn = form =>
            {
                Assert.Equal(new[] { 5, 2 }, form.Rows.Select(r => r.ReturnableQuantity));
                Assert.Equal("0,00 DA", form.RefundText);

                form.Rows[0].QuantityText = "2";
                Assert.Equal("1 041,00 DA", form.RefundText);
                form.Rows[1].QuantityText = "1";
                Assert.Equal("1 076,00 DA", form.RefundText);

                form.ConfirmCommand.Execute(null);
            };
            _screen.ReturnItemsCommand.Execute(null);

            Assert.Contains("1 076,00 DA à rembourser", _screen.StatusMessage);
            Assert.True(_screen.HasReturns);
            Assert.Equal("Retours : 1 076,00 DA remboursés", _screen.RefundedText);
            Assert.Equal(new[] { 2, 1 }, _screen.Lines.Select(l => l.ReturnedQuantity));
            Assert.Equal(new[] { "Café moulu", "Eau minérale" }, _screen.ReturnRows.Select(r => r.ProductName));
            Assert.Equal(new long[] { 104100, 3500 }, _screen.ReturnRows.Select(r => r.AmountCents));
            Assert.Equal(7, _products.GetById(_coffeeId).StockQuantity);
            Assert.Equal(23, _products.GetById(_waterId).StockQuantity);

            // A sale with a return can no longer be cancelled, but what is left can still be returned.
            Assert.False(_screen.CancelSaleCommand.CanExecute(null));
            Assert.True(_screen.ReturnItemsCommand.CanExecute(null));
        }

        [Fact]
        public void A_second_return_only_offers_what_is_left_and_then_nothing_can_be_returned()
        {
            Sell(coffee: 2, water: 1);
            SelectFirst();
            _dialogs.OnReturn = form =>
            {
                form.Rows[0].QuantityText = "2";
                form.ConfirmCommand.Execute(null);
            };
            _screen.ReturnItemsCommand.Execute(null);

            _dialogs.OnReturn = form =>
            {
                // The coffee line is entirely returned: only the water is offered.
                Assert.Equal("Eau minérale", form.Rows.Single().Name);
                form.ReturnAllCommand.Execute(null);
                Assert.Equal("1", form.Rows.Single().QuantityText);
                form.ConfirmCommand.Execute(null);
            };
            _screen.ReturnItemsCommand.Execute(null);

            Assert.False(_screen.ReturnItemsCommand.CanExecute(null));
            Assert.Equal(10, _products.GetById(_coffeeId).StockQuantity);
            Assert.Equal(24, _products.GetById(_waterId).StockQuantity);
            Assert.Equal("Retours : 1 076,00 DA remboursés", _screen.RefundedText);
        }

        [Fact]
        public void A_quantity_beyond_what_can_be_returned_is_refused_in_the_window()
        {
            Sell(coffee: 2, water: 1);
            SelectFirst();

            _dialogs.OnReturn = form =>
            {
                form.ConfirmCommand.Execute(null);
                Assert.Null(form.Result);
                Assert.Equal("Indiquez la quantité retournée d'au moins un article.", form.Error);

                form.Rows[0].QuantityText = "3";
                Assert.True(form.Rows[0].HasError);
                Assert.Null(form.Error);
                form.ConfirmCommand.Execute(null);
                Assert.Null(form.Result);
                Assert.Contains("entier de 0 à 2", form.Error);

                form.Rows[0].QuantityText = "abc";
                form.ConfirmCommand.Execute(null);
                Assert.Null(form.Result);

                // Escape: the return is abandoned.
            };
            _screen.ReturnItemsCommand.Execute(null);

            Assert.Null(_screen.StatusMessage);
            Assert.False(_screen.HasReturns);
            Assert.Equal(8, _products.GetById(_coffeeId).StockQuantity);
        }

        [Fact]
        public void Cancelling_asks_for_confirmation_first()
        {
            Sell(coffee: 2, water: 1);
            SelectFirst();

            _dialogs.ConfirmAnswer = false;
            _screen.CancelSaleCommand.Execute(null);

            Assert.Single(_dialogs.Confirmations);
            Assert.Equal(Sale.StatusValidated, _screen.SelectedSale.Status);
            Assert.Equal(8, _products.GetById(_coffeeId).StockQuantity);
        }

        [Fact]
        public void A_confirmed_cancellation_puts_the_stock_back_and_locks_the_sale()
        {
            var sale = Sell(coffee: 2, water: 1);
            SelectFirst();

            _dialogs.ConfirmAnswer = true;
            _screen.CancelSaleCommand.Execute(null);

            Assert.Contains(sale.Number, _dialogs.Confirmations.Single());
            Assert.Equal(Sale.StatusCancelled, _screen.SelectedSale.Status);
            Assert.EndsWith(" — Annulée", _screen.DetailTitle);
            Assert.Contains("annulée", _screen.StatusMessage);
            Assert.Equal("1 vente — dont 0 validée pour 0,00 DA", _screen.SummaryText);
            Assert.Equal(10, _products.GetById(_coffeeId).StockQuantity);
            Assert.Equal(24, _products.GetById(_waterId).StockQuantity);
            Assert.False(_screen.CancelSaleCommand.CanExecute(null));
            Assert.False(_screen.ReturnItemsCommand.CanExecute(null));
        }

        [Fact]
        public void The_receipt_of_a_sale_is_reprinted_with_its_lines()
        {
            var sale = Sell(coffee: 2, water: 1);
            SelectFirst();
            _receipts.IsEnabled = true;

            _screen.ReprintCommand.Execute(null);

            var printed = _receipts.Printed.Single();
            Assert.Equal(sale.Number, printed.Number);
            Assert.Equal(sale.CreatedAtUtc, printed.CreatedAtUtc);
            Assert.Equal(107600, printed.TotalCents);
            Assert.Equal(500000, printed.ReceivedCents);
            Assert.Equal(392400, printed.ChangeCents);
            Assert.Equal(new[] { "Café moulu", "Eau minérale" }, printed.Lines.Select(l => l.ProductName));
            Assert.Contains("envoyé à l'imprimante", _screen.StatusMessage);
        }

        [Fact]
        public void Reprinting_without_a_receipt_printer_says_where_to_set_it()
        {
            Sell(coffee: 1, water: 0);
            SelectFirst();
            _receipts.IsEnabled = false;

            _screen.ReprintCommand.Execute(null);

            Assert.Empty(_receipts.Printed);
            Assert.Contains("Paramètres", _dialogs.Warnings.Single());
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

            return _saleService.Validate(cart, 500000);
        }

        private void SelectFirst()
        {
            _screen.Refresh();
            _screen.SelectedSale = _screen.Sales.First();
        }

        private sealed class FakeDialogs : IDialogService
        {
            public Action<ReturnViewModel> OnReturn { get; set; }

            public bool ConfirmAnswer { get; set; }

            public List<string> Confirmations { get; } = new List<string>();

            public List<string> Warnings { get; } = new List<string>();

            public bool ShowReturn(ReturnViewModel viewModel)
            {
                OnReturn?.Invoke(viewModel);
                return viewModel.Result != null;
            }

            public bool Confirm(string message)
            {
                Confirmations.Add(message);
                return ConfirmAnswer;
            }

            public void ShowWarning(string message)
            {
                Warnings.Add(message);
            }

            public bool ShowPayment(PaymentViewModel viewModel)
            {
                return false;
            }

            public bool Ask(string headline, string question)
            {
                return false;
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
