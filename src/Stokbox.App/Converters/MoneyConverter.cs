using System;
using System.Globalization;
using System.Windows.Data;
using Stokbox.Core;

namespace Stokbox.App.Converters
{
    /// <summary>
    /// Centimes -> "1 250,00 DA".
    /// </summary>
    public sealed class MoneyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is long cents ? Money.Format(cents) : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
