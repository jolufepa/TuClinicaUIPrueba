using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TuClinica.Core.Enums; // Asegúrate de tener este using

namespace TuClinica.UI.Converters
{
    /// <summary>
    /// Convierte ToothCondition a Visibility.
    /// Muestra el elemento si la condición es "Ausente".
    /// </summary>
    public class ConditionIsAusenteToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ToothCondition condition)
            {
                return condition == ToothCondition.Ausente ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}