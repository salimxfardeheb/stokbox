using System;
using System.Collections.Generic;
using Stokbox.Core.Services;
using Stokbox.Core.Tests.Fakes;
using Xunit;

namespace Stokbox.Core.Tests
{
    public class BarcodeGeneratorTests
    {
        [Theory]
        [InlineData("400638133393", '1')]
        [InlineData("590123412345", '7')]
        [InlineData("978030640615", '7')]
        [InlineData("001234567890", '5')]
        [InlineData("978316148410", '0')]
        [InlineData("200000000001", '5')]
        public void Check_digit_matches_known_EAN13_codes(string first12Digits, char expected)
        {
            Assert.Equal(expected, BarcodeGenerator.ComputeCheckDigit(first12Digits));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("40063813339")]
        [InlineData("4006381333931")]
        [InlineData("40063813339A")]
        public void Check_digit_requires_exactly_12_digits(string first12Digits)
        {
            Assert.Throws<ArgumentException>(() => BarcodeGenerator.ComputeCheckDigit(first12Digits));
        }

        [Theory]
        [InlineData("4006381333931")]
        [InlineData("5901234123457")]
        [InlineData("9780306406157")]
        [InlineData("0012345678905")]
        [InlineData("9783161484100")]
        [InlineData("2000000000015")]
        public void Valid_EAN13_codes_are_accepted(string code)
        {
            Assert.True(BarcodeGenerator.IsValidEan13(code));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("4006381333932")]
        [InlineData("400638133393")]
        [InlineData("40063813339310")]
        [InlineData("400638133393A")]
        [InlineData(" 4006381333931")]
        [InlineData("٤٠٠٦٣٨١٣٣٣٩٣١")]
        public void Invalid_EAN13_codes_are_rejected(string code)
        {
            Assert.False(BarcodeGenerator.IsValidEan13(code));
        }

        [Fact]
        public void Code_is_the_prefix_20_then_the_sequence_on_10_digits_then_the_check_digit()
        {
            Assert.Equal("2000000000015", BarcodeGenerator.FromSequenceValue(1));
            Assert.Equal("2000000001234", BarcodeGenerator.FromSequenceValue(123));
            Assert.StartsWith("209999999999", BarcodeGenerator.FromSequenceValue(BarcodeGenerator.MaxSequenceValue));
        }

        [Theory]
        [InlineData(0L)]
        [InlineData(-1L)]
        [InlineData(10000000000L)]
        public void Sequence_value_must_fit_in_10_digits(long value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BarcodeGenerator.FromSequenceValue(value));
        }

        [Fact]
        public void A_thousand_codes_generated_in_a_row_are_unique_and_valid()
        {
            var generator = new BarcodeGenerator(new InMemoryBarcodeSequence());
            var codes = new HashSet<string>();

            for (var i = 0; i < 1000; i++)
            {
                var code = generator.Next();

                Assert.True(BarcodeGenerator.IsValidEan13(code), code);
                Assert.StartsWith(BarcodeGenerator.Prefix, code);
                Assert.True(codes.Add(code), "Code en double : " + code);
            }

            Assert.Equal(1000, codes.Count);
        }

        [Fact]
        public void Generation_continues_after_the_last_value_of_the_sequence()
        {
            var generator = new BarcodeGenerator(new InMemoryBarcodeSequence(41));

            Assert.Equal(BarcodeGenerator.FromSequenceValue(42), generator.Next());
        }
    }
}
