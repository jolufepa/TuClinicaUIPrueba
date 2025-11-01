using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

// 1. Asegúrate de que el namespace sea EXACTAMENTE éste:
namespace TuClinica.UI.Converters
{
    // 2. Esta es la clase que App.xaml está buscando
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Si es true, Visible. Si es false, Collapsed.
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            // Si no es un booleano, ocúltalo por defecto.
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // No necesitamos convertir de vuelta
            throw new NotImplementedException();
        }
    }
}
