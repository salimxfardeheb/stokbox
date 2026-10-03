using System;
using System.Linq;
using Stokbox.Core.Services;
using Stokbox.Core.Tests.Fakes;
using Stokbox.Core.Validation;
using Xunit;

namespace Stokbox.Core.Tests
{
    public class StockServiceTests
    {
        private readonly InMemoryProductRepository _products = new InMemoryProductRepository();
        private readonly InMemoryStockMovementRepository _movements;
        private readonly StockService _service;
        private readonly long _productId;

        public StockServiceTests()
        {
            _movements = new InMemoryStockMovementRepository(_products);
            _service = new StockService(_products, _movements);
            _productId = _products.Insert("2000000000015", "Café moulu", 1, 40000, 52050, DateTime.UtcNow);
        }

        [Fact]
        public void An_entry_records_a_dated_movement_and_reports_the_new_stock()
        {
            var before = DateTime.UtcNow;

            var entry = _service.AddEntry(_productId, 12);

            Assert.Equal(1, _movements.Count);
            Assert.Equal(12, entry.Quantity);
            Assert.Equal("Café moulu", entry.Product.Name);
            Assert.Equal(DateTimeKind.Utc, entry.CreatedAtUtc.Kind);
            Assert.InRange(entry.CreatedAtUtc, before, DateTime.UtcNow);
            Assert.Equal(entry.CreatedAtUtc, _movements.LastCreatedAtUtc);
        }

        [Fact]
        public void Stock_is_the_sum_of_the_entries()
        {
            var first = _service.AddEntry(_productId, 10);
            var second = _service.AddEntry(_productId, 5);
            var third = _service.AddEntry(_productId, 1);

            Assert.Equal(new long[] { 10, 15, 16 }, new[] { first, second, third }.Select(e => e.StockQuantityAfter));
            Assert.Equal(16, _products.GetById(_productId).StockQuantity);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-50)]
        public void A_quantity_of_zero_or_less_is_refused(int quantity)
        {
            var error = Assert.Throws<ValidationException>(() => _service.AddEntry(_productId, quantity));

            Assert.Equal(StockService.QuantityField, error.Errors.Single().Field);
            Assert.Equal(0, _movements.Count);
        }

        [Fact]
        public void A_quantity_above_the_maximum_is_refused()
        {
            Assert.Throws<ValidationException>(() => _service.AddEntry(_productId, StockService.MaxEntryQuantity + 1));
            Assert.Equal(0, _movements.Count);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(StockService.MaxEntryQuantity)]
        public void A_quantity_between_1_and_the_maximum_is_accepted(int quantity)
        {
            Assert.Equal(quantity, _service.AddEntry(_productId, quantity).StockQuantityAfter);
        }

        [Fact]
        public void An_entry_on_an_archived_product_is_refused()
        {
            _products.SetArchived(_productId, true);

            Assert.Throws<BusinessRuleException>(() => _service.AddEntry(_productId, 5));
            Assert.Equal(0, _movements.Count);
        }

        [Fact]
        public void An_entry_on_an_unarchived_product_is_accepted_again()
        {
            _products.SetArchived(_productId, true);
            _products.SetArchived(_productId, false);

            _service.AddEntry(_productId, 5);

            Assert.Equal(1, _movements.Count);
        }

        [Fact]
        public void An_entry_on_a_missing_product_is_refused()
        {
            Assert.Throws<BusinessRuleException>(() => _service.AddEntry(999, 5));
            Assert.Equal(0, _movements.Count);
        }
    }
}
