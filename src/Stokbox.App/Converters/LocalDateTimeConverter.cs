using System;
using System.Globalization;
using System.Windows.Data;

namespace Stokbox.App.Converters
{
    /// <summary>
    /// UTC date as stored -> local "03/10/2026 14:05".
    /// </summary>
    public sealed class LocalDateTimeConverter : IValueConverter
    {
        public const string DisplayFormat = "dd/MM/yyyy HH:mm";

        public static string Format(DateTime utc)
        {
            return utc.ToLocalTime().ToString(DisplayFormat, CultureInfo.InvariantCulture);
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is DateTime utc ? Format(utc) : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
