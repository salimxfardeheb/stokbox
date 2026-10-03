using Stokbox.Core.Entities;
using Xunit;

namespace Stokbox.Core.Tests
{
    public class ProductTests
    {
        [Fact]
        public void Margin_is_the_sale_price_minus_the_purchase_price()
        {
            var product = new Product { PurchasePriceCents = 100000, SalePriceCents = 125000 };

            Assert.Equal(25000, product.MarginCents);
            Assert.Equal(25.0m, product.MarginPercent);
        }

        [Fact]
        public void Margin_is_negative_when_selling_below_the_purchase_price()
        {
            var product = new Product { PurchasePriceCents = 30000, SalePriceCents = 20000 };

            Assert.Equal(-10000, product.MarginCents);
            Assert.Equal(-33.3m, product.MarginPercent);
        }

        [Fact]
        public void Margin_percent_is_undefined_without_a_purchase_price()
        {
            var product = new Product { PurchasePriceCents = 0, SalePriceCents = 20000 };

            Assert.Equal(20000, product.MarginCents);
            Assert.Null(product.MarginPercent);
        }
    }
}
