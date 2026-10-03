using System.Globalization;
using System.Text;

namespace Stokbox.Core
{
    public static class TextNormalizer
    {
        /// <summary>
        /// Form used to compare texts regardless of case and accents: "Café crème" -> "CAFE CREME".
        /// </summary>
        public static string Fold(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var decomposed = text.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        }
    }
}
