using System;
using System.Linq;
using Stokbox.Core.Entities;
using Stokbox.Core.Services;
using Stokbox.Core.Tests.Fakes;
using Stokbox.Core.Validation;
using Xunit;

namespace Stokbox.Core.Tests
{
    public class SaleHistoryServiceTests
    {
        private static readonly DateTime Noon = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

        private readonly InMemorySaleRepository _sales = new InMemorySaleRepository();
        private readonly SaleService _service;

        public SaleHistoryServiceTests()
        {
            _service = new SaleService(new InMemoryProductRepository(), _sales, () => Noon);
        }

        [Fact]
        public void A_return_without_any_line_is_refused()
        {
            Assert.Throws<ValidationException>(() => _service.ReturnItems(1, new ReturnRequestLine[0]));
            Assert.Throws<ValidationException>(() => _service.ReturnItems(1, null));
            Assert.Empty(_sales.ReturnsAsked);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void A_returned_quantity_of_zero_or_less_is_refused(int quantity)
        {
            var lines = new[] { new ReturnRequestLine(10, 1), new ReturnRequestLine(11, quantity) };

            Assert.Throws<ValidationException>(() => _service.ReturnItems(1, lines));
            Assert.Empty(_sales.ReturnsAsked);
        }

        [Fact]
        public void The_same_line_given_twice_is_returned_once_for_the_sum()
        {
            _service.ReturnItems(7, new[]
            {
                new ReturnRequestLine(10, 1),
                new ReturnRequestLine(11, 2),
                new ReturnRequestLine(10, 3)
            });

            var asked = _sales.ReturnsAsked.Single();
            Assert.Equal(7, asked.Key);
            Assert.Equal(new long[] { 10, 11 }, asked.Value.Select(l => l.SaleLineId));
            Assert.Equal(new[] { 4, 2 }, asked.Value.Select(l => l.Quantity));
        }

        [Fact]
        public void A_return_is_dated()
        {
            var saleReturn = _service.ReturnItems(7, new[] { new ReturnRequestLine(10, 1) });

            Assert.Equal(Noon, saleReturn.CreatedAtUtc);
        }

        [Fact]
        public void Cancelling_goes_to_the_repository()
        {
            _service.CancelSale(7);

            Assert.Equal(new long[] { 7 }, _sales.Cancelled);
        }

        [Fact]
        public void A_period_covers_its_two_local_days_entirely_whatever_their_order()
        {
            var first = new DateTime(2026, 10, 1, 15, 30, 0);
            var last = new DateTime(2026, 10, 3, 9, 0, 0);

            _service.SearchSales(last, first, "  ");

            Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Local).ToUniversalTime(), _sales.LastSearchFromUtc);
            Assert.Equal(new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Local).ToUniversalTime(), _sales.LastSearchToUtc);
            Assert.Null(_sales.LastSearchNumber);
        }

        [Fact]
        public void A_number_is_searched_trimmed()
        {
            _service.SearchSales(Noon, Noon, " v-2026 ");

            Assert.Equal("v-2026", _sales.LastSearchNumber);
        }

        [Fact]
        public void The_amount_to_refund_is_the_quantities_times_the_frozen_prices()
        {
            var saleReturn = new SaleReturn
            {
                Lines = new[]
                {
                    new SaleReturnLine { Quantity = 2, UnitPriceCents = 52050 },
                    new SaleReturnLine { Quantity = 1, UnitPriceCents = 3500 }
                }
            };

            Assert.Equal(new long[] { 104100, 3500 }, saleReturn.Lines.Select(l => l.AmountCents));
            Assert.Equal(107600, saleReturn.RefundCents);
        }

        [Fact]
        public void A_sale_line_can_still_return_what_was_sold_minus_what_came_back()
        {
            var line = new SaleLine { Quantity = 5, ReturnedQuantity = 2 };

            Assert.Equal(3, line.ReturnableQuantity);
        }
    }
}
