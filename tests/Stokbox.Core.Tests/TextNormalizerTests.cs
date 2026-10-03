using Xunit;

namespace Stokbox.Core.Tests
{
    public class TextNormalizerTests
    {
        [Theory]
        [InlineData("Café crème", "CAFE CREME")]
        [InlineData("CAFÉ", "CAFE")]
        [InlineData("Ça, où, île, Noël", "CA, OU, ILE, NOEL")]
        [InlineData("abc 123", "ABC 123")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void Fold_removes_accents_and_case(string text, string expected)
        {
            Assert.Equal(expected, TextNormalizer.Fold(text));
        }
    }
}
