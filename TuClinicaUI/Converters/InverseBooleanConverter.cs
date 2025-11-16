using System;
using System.Globalization;
using System.Windows; // <-- ¡ASEGÚRATE DE QUE ESTE USING ESTÉ PRESENTE!
using System.Windows.Data; // Para IValueConverter

namespace TuClinica.UI.Converters // Asegúrate que el namespace es .UI.Converters
{
    // Convierte un booleano a su valor INVERSO (true->false, false->true)
    // Útil para deshabilitar algo cuando una condición es verdadera.
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // --- INICIO DE LA CORRECCIÓN ---
            bool boolValue = false;
            if (value is bool)
            {
                boolValue = (bool)value;
            }

            // Si el valor es TRUE (modo solo lectura), devolvemos COLLAPSED (oculto)
            // Si el valor es FALSE (modo edición), devolvemos VISIBLE
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
            // --- FIN DE LA CORRECCIÓN ---
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // No necesitamos convertir de vuelta
            throw new NotImplementedException();
        }
    }
}