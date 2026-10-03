using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Stokbox.Core
{
    /// <summary>
    /// Amounts in Algerian dinar: stored as integer centimes (RG-09), typed and displayed in DA.
    /// </summary>
    public static class Money
    {
        public const string CurrencySymbol = "DA";

        private static readonly Regex AmountPattern = new Regex(@"^-?[0-9]+([.,][0-9]+)?$", RegexOptions.CultureInvariant);

        private static readonly NumberFormatInfo GroupedBySpaces = new NumberFormatInfo
        {
            NumberGroupSeparator = " ",
            NumberGroupSizes = new[] { 3 }
        };

        /// <summary>
        /// Reads an amount typed with a comma or a point as decimal separator.
        /// Spaces and a trailing "DA" are ignored. The number of decimals is not limited here.
        /// </summary>
        public static bool TryParse(string text, out decimal amount)
        {
            amount = 0m;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var cleaned = text
                .Replace(" ", string.Empty)
                .Replace(" ", string.Empty)
                .Replace(" ", string.Empty);
            if (cleaned.EndsWith(CurrencySymbol, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(0, cleaned.Length - CurrencySymbol.Length);
            }

            if (!AmountPattern.IsMatch(cleaned))
            {
                return false;
            }

            return decimal.TryParse(
                cleaned.Replace(',', '.'),
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out amount);
        }

        public static bool HasAtMostTwoDecimals(decimal amount)
        {
            return decimal.Round(amount, 2) == amount;
        }

        public static long ToCents(decimal amount)
        {
            return (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Display format: "1 250,00 DA".
        /// </summary>
        public static string Format(long cents)
        {
            return FormatNumber(cents, true) + " " + CurrencySymbol;
        }

        /// <summary>
        /// Format used to pre-fill an input field: "1250,00".
        /// </summary>
        public static string FormatForInput(long cents)
        {
            return FormatNumber(cents, false);
        }

        private static string FormatNumber(long cents, bool grouped)
        {
            var absolute = Math.Abs((decimal)cents);
            var whole = decimal.Truncate(absolute / 100m);
            var fraction = (int)(absolute % 100m);

            return (cents < 0 ? "-" : string.Empty)
                + whole.ToString(grouped ? "#,0" : "0", GroupedBySpaces)
                + ","
                + fraction.ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
