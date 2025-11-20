using System;
using System.Globalization;
using System.Windows.Data;

namespace TuClinica.UI.Converters
{
    public class CurrencyDashConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal amount)
            {
                if (amount == 0) return "- €";
                return amount.ToString("C", new CultureInfo("es-ES"));
            }
            return "- €";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}