using Xunit;

namespace Stokbox.Core.Tests
{
    public class MoneyTests
    {
        [Theory]
        [InlineData("1250", 125000L)]
        [InlineData("1250,5", 125050L)]
        [InlineData("1250.50", 125050L)]
        [InlineData("1250,00", 125000L)]
        [InlineData("1 250,00 DA", 125000L)]
        [InlineData("1 250,00 DA", 125000L)]
        [InlineData("  0,05 da ", 5L)]
        [InlineData("0", 0L)]
        public void Amounts_are_read_with_a_comma_or_a_point(string text, long expectedCents)
        {
            Assert.True(Money.TryParse(text, out var amount));
            Assert.Equal(expectedCents, Money.ToCents(amount));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("abc")]
        [InlineData("12,50,3")]
        [InlineData("1.250,00")]
        [InlineData(",50")]
        [InlineData("12,")]
        [InlineData("12 EUR")]
        [InlineData("DA")]
        public void Text_that_is_not_an_amount_is_rejected(string text)
        {
            Assert.False(Money.TryParse(text, out _));
        }

        [Fact]
        public void Parsing_keeps_extra_decimals_so_that_they_can_be_refused()
        {
            Assert.True(Money.TryParse("12,345", out var amount));

            Assert.Equal(12.345m, amount);
            Assert.False(Money.HasAtMostTwoDecimals(amount));
        }

        [Theory]
        [InlineData(125000L, "1 250,00 DA")]
        [InlineData(5L, "0,05 DA")]
        [InlineData(0L, "0,00 DA")]
        [InlineData(99950L, "999,50 DA")]
        [InlineData(123456789L, "1 234 567,89 DA")]
        [InlineData(-125050L, "-1 250,50 DA")]
        public void Amounts_are_displayed_with_spaces_a_comma_and_the_currency(long cents, string expected)
        {
            Assert.Equal(expected, Money.Format(cents));
        }

        [Theory]
        [InlineData(125000L, "1250,00")]
        [InlineData(5L, "0,05")]
        public void Input_format_has_no_grouping_and_no_currency(long cents, string expected)
        {
            Assert.Equal(expected, Money.FormatForInput(cents));
        }

        [Fact]
        public void A_displayed_amount_can_be_read_back()
        {
            Assert.True(Money.TryParse(Money.Format(123456789), out var amount));

            Assert.Equal(123456789L, Money.ToCents(amount));
        }
    }
}
