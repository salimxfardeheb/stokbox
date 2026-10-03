using System;
using System.Globalization;
using System.Windows.Data;

namespace Stokbox.App.Converters
{
    /// <summary>
    /// 25.0 -> "25,0 %"; no value -> "—".
    /// </summary>
    public sealed class PercentConverter : IValueConverter
    {
        private static readonly NumberFormatInfo CommaDecimal = new NumberFormatInfo { NumberDecimalSeparator = "," };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is decimal percent ? percent.ToString("0.0", CommaDecimal) + " %" : "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
