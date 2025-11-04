using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TuClinica.Core.Enums;

namespace TuClinica.UI.Converters
{
    /// <summary>
    /// Convierte el ToothCondition (patología) a un color de fondo para la superficie dental.
    /// </summary>
    public class ConditionToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ToothCondition condition)
            {
                // Un SolidColorBrush para representar el color
                switch (condition)
                {
                    case ToothCondition.Sano:
                        return new SolidColorBrush(Colors.White); // Fondo blanco/hueso para sano
                    case ToothCondition.Caries:
                        return new SolidColorBrush(Colors.Red); // Rojo estándar para caries
                    case ToothCondition.ExtraccionIndicada:
                        // Se podría usar un color más suave o un patrón de fondo
                        return new SolidColorBrush(Color.FromArgb(255, 255, 128, 0)); // Naranja
                    case ToothCondition.Fractura:
                        return new SolidColorBrush(Colors.Yellow); // Amarillo para fractura/fisura
                    case ToothCondition.Ausente:
                        return new SolidColorBrush(Colors.Transparent); // Transparente o negro para diente ausente
                    default:
                        return new SolidColorBrush(Colors.White);
                }
            }
            // Retorna un color por defecto si el valor no es válido
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}