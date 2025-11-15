using System;
using System.Globalization;
using System.Windows; // <-- Asegúrate de que este 'using' esté
using System.Windows.Data;

namespace TuClinica.UI.Converters
{
    public class IsGreaterThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = false;

            // Comprueba si es un decimal
            if (value is decimal decimalValue)
            {
                isVisible = decimalValue > 0;
            }
            // --- INICIO DE LA MODIFICACIÓN ---
            // Comprueba si es un entero (para nuestro contador)
            else if (value is int intValue)
            {
                isVisible = intValue > 0;
            }
            // --- FIN DE LA MODIFICACIÓN ---

            // Devuelve el valor de Visibilidad correcto
            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}