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
            if (value is not decimal saldo) return Brushes.White; // Blanco por defecto

            // --- LÓGICA DE COLOR CORREGIDA Y LEGIBLE ---

            // saldo > 0: El paciente DEBE dinero (Deuda).
            // Usamos Amarillo brillante, que contrasta perfectamente con el azul.
            if (saldo > 0) return Brushes.Yellow;

            // saldo <= 0: Saldado (0) o A Favor (negativo).
            // Usamos Blanco puro, el más legible.
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}