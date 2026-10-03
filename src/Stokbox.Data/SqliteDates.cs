using System;
using System.Globalization;

namespace Stokbox.Data
{
    internal static class SqliteDates
    {
        /// <summary>
        /// Dates are stored as UTC ISO-8601 text: "2026-03-09T14:05:07.120Z".
        /// </summary>
        public static string ToText(DateTime value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads a stored date back as a UTC DateTime.
        /// </summary>
        public static DateTime FromText(string text)
        {
            return DateTime.Parse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }
    }
}
