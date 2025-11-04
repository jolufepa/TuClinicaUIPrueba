using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TuClinica.Core.Enums;

namespace TuClinica.UI.Converters
{
    /// <summary>
    /// Convierte el ToothRestoration (tratamiento realizado) a un color para la capa superpuesta (Rectangle).
    /// </summary>
    public class RestorationToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ToothRestoration restoration)
            {
                switch (restoration)
                {
                    case ToothRestoration.Ninguna:
                        return new SolidColorBrush(Colors.Transparent); // No hay restauración visible
                    case ToothRestoration.Obturacion:
                        return new SolidColorBrush(Colors.Blue); // Azul o color de resina/amalgama genérico
                    case ToothRestoration.Sellador:
                        return new SolidColorBrush(Colors.LightGreen); // Verde suave para sellador
                    case ToothRestoration.Corona:
                        return new SolidColorBrush(Colors.Gold); // Dorado/Amarillo fuerte para corona
                    case ToothRestoration.Endodoncia:
                        // La Endodoncia es un tratamiento interno, pero si se quiere marcar la cara:
                        return new SolidColorBrush(Color.FromArgb(255, 139, 69, 19)); // Marrón (ej. obturación temporal)
                    case ToothRestoration.Implante:
                        return new SolidColorBrush(Colors.Gray); // Gris metálico
                    default:
                        return new SolidColorBrush(Colors.Transparent);
                }
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}