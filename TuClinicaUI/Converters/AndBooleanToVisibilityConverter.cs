using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace TuClinica.UI.Converters
{
    /// <summary>
    /// MultiConverter que devuelve Visible solo si TODOS los bindings son True (o Visible).
    /// </summary>
    public class AndBooleanToVisibilityConverter : IMultiValueConverter
    {
        // Instancia estática para fácil acceso en XAML
        public static readonly AndBooleanToVisibilityConverter Instance = new AndBooleanToVisibilityConverter();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0)
                return Visibility.Collapsed;

            foreach (var value in values)
            {
                bool boolValue = false;
                if (value is bool b)
                {
                    boolValue = b;
                }
                else if (value is Visibility v)
                {
                    boolValue = (v == Visibility.Visible);
                }
                else if (value == null)
                {
                    boolValue = false;
                }
                else
                {
                    // Intento genérico de conversión
                    try
                    {
                        boolValue = System.Convert.ToBoolean(value);
                    }
                    catch (Exception)
                    {
                        boolValue = false;
                    }
                }


                if (!boolValue)
                {
                    return Visibility.Collapsed;
                }
            }

            return Visibility.Visible;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}