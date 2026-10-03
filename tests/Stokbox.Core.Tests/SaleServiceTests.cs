using System;
using System.Linq;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;
using Stokbox.Core.Tests.Fakes;
using Stokbox.Core.Validation;
using Xunit;

namespace Stokbox.Core.Tests
{
    public class SaleServiceTests
    {
        private static readonly DateTime Noon = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

        private readonly InMemoryProductRepository _products = new InMemoryProductRepository();
        private readonly InMemorySaleRepository _sales = new InMemorySaleRepository();
        private readonly SaleService _service;
        private readonly Cart _cart = new Cart();
        private readonly long _coffeeId;
        private readonly long _waterId;

        public SaleServiceTests()
        {
            _service = new SaleService(_products, _sales, () => Noon);

            _coffeeId = _products.Insert("2000000000015", "Café moulu", 1, 40000, 52050, Noon);
            _products.SetStockQuantity(_coffeeId, 3);
            _waterId = _products.Insert("2000000000022", "Eau minérale", 1, 2500, 3500, Noon);
            _products.SetStockQuantity(_waterId, 24);
        }

        // Panier.

        [Fact]
        public void Adding_a_product_creates_a_line_of_one_unit_at_its_sale_price()
        {
            var line = _service.AddProduct(_cart, _coffeeId);

            Assert.Same(line, Assert.Single(_cart.Lines));
            Assert.Equal("Café moulu", line.Name);
            Assert.Equal("2000000000015", line.Barcode);
            Assert.Equal(1, line.Quantity);
            Assert.Equal(52050, line.UnitPriceCents);
            Assert.Equal(40000, line.UnitPurchasePriceCents);
            Assert.Equal(52050, _cart.TotalCents);
        }

        [Fact]
        public void Scanning_the_same_product_again_adds_one_to_its_line()
        {
            _service.AddProduct(_cart, _coffeeId);
            _service.AddProduct(_cart, _waterId);
            _service.AddProduct(_cart, _coffeeId);

            Assert.Equal(new[] { _coffeeId, _waterId }, _cart.Lines.Select(l => l.ProductId));
            Assert.Equal(new[] { 2, 1 }, _cart.Lines.Select(l => l.Quantity));
            Assert.Equal(3, _cart.ArticleCount);
            Assert.Equal(2 * 52050 + 3500, _cart.TotalCents);
        }

        [Fact]
        public void An_archived_product_cannot_be_added()
        {
            _products.SetArchived(_coffeeId, true);

            Assert.Throws<BusinessRuleException>(() => _service.AddProduct(_cart, _coffeeId));
            Assert.True(_cart.IsEmpty);
        }

        [Fact]
        public void A_missing_product_cannot_be_added()
        {
            Assert.Throws<BusinessRuleException>(() => _service.AddProduct(_cart, 999));
            Assert.True(_cart.IsEmpty);
        }

        // RG-10.

        [Fact]
        public void The_quantity_of_a_product_cannot_exceed_its_stock_when_scanning()
        {
            _service.AddProduct(_cart, _coffeeId);
            _service.AddProduct(_cart, _coffeeId);
            _service.AddProduct(_cart, _coffeeId);

            var error = Assert.Throws<InsufficientStockException>(() => _service.AddProduct(_cart, _coffeeId));

            Assert.Equal("Stock insuffisant : 3 disponible(s)", error.Message);
            Assert.Equal(3, _cart.Lines.Single().Quantity);
        }

        [Fact]
        public void A_product_without_stock_cannot_be_added()
        {
            _products.SetStockQuantity(_coffeeId, 0);

            var error = Assert.Throws<InsufficientStockException>(() => _service.AddProduct(_cart, _coffeeId));

            Assert.Equal("Stock insuffisant : 0 disponible(s)", error.Message);
            Assert.True(_cart.IsEmpty);
        }

        [Fact]
        public void The_quantity_of_a_line_can_be_set_up_to_the_stock()
        {
            _service.AddProduct(_cart, _coffeeId);

            _service.SetQuantity(_cart, _coffeeId, 3);

            Assert.Equal(3, _cart.Lines.Single().Quantity);
            Assert.Equal(3 * 52050, _cart.TotalCents);
        }

        [Fact]
        public void The_quantity_of_a_line_cannot_be_set_above_the_stock()
        {
            _service.AddProduct(_cart, _coffeeId);

            var error = Assert.Throws<InsufficientStockException>(() => _service.SetQuantity(_cart, _coffeeId, 4));

            Assert.Equal("Stock insuffisant : 3 disponible(s)", error.Message);
            Assert.Equal(3, error.Available);
            Assert.Equal(1, _cart.Lines.Single().Quantity);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void The_quantity_of_a_line_must_be_at_least_one(int quantity)
        {
            _service.AddProduct(_cart, _coffeeId);

            Assert.Throws<ValidationException>(() => _service.SetQuantity(_cart, _coffeeId, quantity));
            Assert.Equal(1, _cart.Lines.Single().Quantity);
        }

        [Fact]
        public void A_line_is_removed()
        {
            _service.AddProduct(_cart, _coffeeId);
            _service.AddProduct(_cart, _waterId);

            _service.RemoveLine(_cart, _coffeeId);

            Assert.Equal(_waterId, _cart.Lines.Single().ProductId);
            Assert.Equal(3500, _cart.TotalCents);
        }

        [Fact]
        public void Refreshing_the_cart_follows_the_products()
        {
            _service.AddProduct(_cart, _coffeeId);
            _service.SetQuantity(_cart, _coffeeId, 3);
            _service.AddProduct(_cart, _waterId);
            Assert.False(_service.Refresh(_cart));

            _products.SetStockQuantity(_coffeeId, 2);
            _products.Update(_coffeeId, "Café moulu 250 g", 1, 41000, 55000);
            _products.SetArchived(_waterId, true);

            Assert.True(_service.Refresh(_cart));

            var line = Assert.Single(_cart.Lines);
            Assert.Equal("Café moulu 250 g", line.Name);
            Assert.Equal(2, line.Quantity);
            Assert.Equal(55000, line.UnitPriceCents);
            Assert.Equal(41000, line.UnitPurchasePriceCents);
        }

        // Validation, RG-08.

        [Fact]
        public void An_empty_cart_cannot_be_validated()
        {
            Assert.Throws<BusinessRuleException>(() => _service.Validate(_cart, 100000));
            Assert.Empty(_sales.Created);
        }

        [Theory]
        [InlineData(0L)]
        [InlineData(55549L)]
        public void The_amount_received_must_cover_the_total(long receivedCents)
        {
            FillCart();

            var error = Assert.Throws<ValidationException>(() => _service.Validate(_cart, receivedCents));

            Assert.Equal(SaleService.ReceivedField, error.Errors.Single().Field);
            Assert.Empty(_sales.Created);
            Assert.Equal(2, _cart.Lines.Count);
        }

        [Fact]
        public void The_exact_amount_is_accepted_with_no_change()
        {
            FillCart();

            var sale = _service.Validate(_cart, 55550);

            Assert.Equal(55550, sale.TotalCents);
            Assert.Equal(55550, sale.ReceivedCents);
            Assert.Equal(0, sale.ChangeCents);
        }

        [Fact]
        public void The_change_is_the_amount_received_minus_the_total()
        {
            FillCart();

            var sale = _service.Validate(_cart, 60000);

            Assert.Equal(55550, sale.TotalCents);
            Assert.Equal(60000, sale.ReceivedCents);
            Assert.Equal(4450, sale.ChangeCents);
        }

        // RG-04.

        [Fact]
        public void The_sale_lines_freeze_the_prices_of_the_cart()
        {
            FillCart();
            _service.SetQuantity(_cart, _waterId, 2);

            var sale = _service.Validate(_cart, 100000);

            var coffee = sale.Lines.Single(l => l.ProductId == _coffeeId);
            Assert.Equal("Café moulu", coffee.ProductName);
            Assert.Equal(1, coffee.Quantity);
            Assert.Equal(52050, coffee.UnitPriceCents);
            Assert.Equal(40000, coffee.UnitPurchasePriceCents);
            Assert.Equal(52050, coffee.LineTotalCents);

            var water = sale.Lines.Single(l => l.ProductId == _waterId);
            Assert.Equal(2, water.Quantity);
            Assert.Equal(3500, water.UnitPriceCents);
            Assert.Equal(2500, water.UnitPurchasePriceCents);
            Assert.Equal(7000, water.LineTotalCents);

            Assert.Equal(59050, sale.TotalCents);
        }

        [Fact]
        public void A_validated_sale_is_dated_and_empties_the_cart()
        {
            FillCart();

            var sale = _service.Validate(_cart, 60000);

            Assert.Equal(Noon, sale.CreatedAtUtc);
            Assert.Equal(Noon.ToLocalTime().Date, _sales.Created.Single().NumberDate);
            Assert.Equal(Sale.StatusValidated, sale.Status);
            Assert.True(_cart.IsEmpty);
        }

        [Fact]
        public void A_sale_refused_by_the_database_keeps_the_cart()
        {
            FillCart();
            _sales.Failure = new InsufficientStockException("Café moulu", 0);

            Assert.Throws<InsufficientStockException>(() => _service.Validate(_cart, 60000));
            Assert.Equal(2, _cart.Lines.Count);
        }

        private void FillCart()
        {
            _service.AddProduct(_cart, _coffeeId);
            _service.AddProduct(_cart, _waterId);
        }
    }
}
