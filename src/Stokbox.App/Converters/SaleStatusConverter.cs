using System;
using System.Globalization;
using System.Windows.Data;
using Stokbox.Core.Entities;

namespace Stokbox.App.Converters
{
    /// <summary>
    /// Stored status -> "Validée" / "Annulée".
    /// </summary>
    public sealed class SaleStatusConverter : IValueConverter
    {
        public static string Format(string status)
        {
            switch (status)
            {
                case Sale.StatusValidated:
                    return "Validée";
                case Sale.StatusCancelled:
                    return "Annulée";
                default:
                    return status ?? string.Empty;
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Format(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
