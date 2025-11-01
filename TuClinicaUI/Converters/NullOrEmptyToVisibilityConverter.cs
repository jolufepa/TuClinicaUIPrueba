using System;
using System.Globalization;
using System.Windows; // Para Visibility
using System.Windows.Data; // Para IValueConverter

namespace TuClinica.UI.Converters // Asegúrate que el namespace es TuClinica.UI...
{
    // Esta clase convierte un string (ErrorMessage) en Visibilidad
    public class NullOrEmptyToVisibilityConverter : IValueConverter
    {
        // Convert: Va del ViewModel (string) a la Vista (Visibility)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Si el valor (el string) es nulo o vacío...
            if (value is string stringValue && string.IsNullOrEmpty(stringValue))
            {
                // ...ocultamos el TextBlock.
                return Visibility.Collapsed;
            }
            // Si tiene texto...
            else
            {
                // ...mostramos el TextBlock.
                return Visibility.Visible;
            }
        }

        // ConvertBack: Va de la Vista al ViewModel (no lo necesitamos aquí)
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // No implementado
        }
    }
}