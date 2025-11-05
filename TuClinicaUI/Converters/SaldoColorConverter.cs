// TuClinica.UI/Converters/SaldoColorConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TuClinica.UI.Converters
{
    [ValueConversion(typeof(decimal), typeof(Brush))]
    public class SaldoColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not decimal saldo) return Brushes.Black;

            return saldo > 0 ? Brushes.Red :
                   saldo < 0 ? Brushes.Blue :
                   Brushes.Green;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}