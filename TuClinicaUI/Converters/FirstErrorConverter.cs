// En: TuClinicaUI/Converters/FirstErrorConverter.cs
using System;
using System.Collections; // Para IEnumerable
using System.Globalization;
using System.Linq; // Para .Cast y .FirstOrDefault
using System.Windows.Controls; // ¡¡Para ValidationError!!
using System.Windows.Data;

namespace TuClinica.UI.Converters
{
    /// <summary>
    /// Convierte una colección de errores (como de INotifyDataErrorInfo)
    /// y devuelve solo el primer mensaje de error.
    /// </summary>
    public class FirstErrorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // El valor 'value' que llega es la colección (Validation.Errors)
            if (value is IEnumerable errors)
            {
                // 1. Convierte la colección a objetos ValidationError
                // 2. Obtiene el primero (o null si la lista está vacía)
                var firstError = errors.Cast<ValidationError>().FirstOrDefault();

                // 3. Devuelve su contenido de error, o null (y el TextBlock no mostrará nada)
                return firstError?.ErrorContent?.ToString();
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}