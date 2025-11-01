using System;
using System.Globalization;
using System.Windows.Data; // Para IValueConverter

namespace TuClinica.UI.Converters // Asegúrate que el namespace es .UI.Converters
{
    // Convierte un booleano a su valor INVERSO (true->false, false->true)
    // Útil para deshabilitar algo cuando una condición es verdadera.
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue; // Devuelve el valor negado
            }
            return true; // Valor por defecto si no es booleano
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // No necesitamos convertir de vuelta
            throw new NotImplementedException();
        }
    }
}