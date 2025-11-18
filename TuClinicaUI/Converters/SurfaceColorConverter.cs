using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TuClinica.Core.Enums;

namespace TuClinica.UI.Converters
{
    public class SurfaceColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not ToothCondition condition || values[1] is not ToothRestoration restoration)
            {
                return new SolidColorBrush(Colors.White);
            }

            // 1. Prioridad: Restauraciones
            switch (restoration)
            {
                case ToothRestoration.Obturacion: return new SolidColorBrush(Color.FromRgb(52, 152, 219)); // Azul
                case ToothRestoration.Corona: return new SolidColorBrush(Color.FromRgb(241, 196, 15)); // Dorado
                case ToothRestoration.Endodoncia: return new SolidColorBrush(Color.FromRgb(155, 89, 182)); // Morado
                case ToothRestoration.Implante: return new SolidColorBrush(Color.FromRgb(149, 165, 166)); // Gris
                case ToothRestoration.Sellador: return new SolidColorBrush(Color.FromRgb(46, 204, 113)); // Verde
                case ToothRestoration.ProtesisFija: return new SolidColorBrush(Color.FromRgb(192, 57, 43)); // Rojo oscuro
                case ToothRestoration.ProtesisRemovible: return new SolidColorBrush(Colors.HotPink);
            }

            // 2. Prioridad: Condiciones
            switch (condition)
            {
                case ToothCondition.Caries: return new SolidColorBrush(Color.FromRgb(231, 76, 60)); // Rojo
                case ToothCondition.Fractura: return new SolidColorBrush(Colors.Orange);
                case ToothCondition.ExtraccionIndicada: return new SolidColorBrush(Colors.OrangeRed);
                // Si es Ausente, devolvemos transparente aquí, porque el Style Trigger se encargará de pintar las líneas
                case ToothCondition.Ausente: return new SolidColorBrush(Colors.Transparent);
            }

            return new SolidColorBrush(Colors.White);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}