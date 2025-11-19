using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TuClinica.Core.Enums;

namespace TuClinica.UI.Converters
{
    /// <summary>
    /// Convierte ToothRestoration a Visibility.
    /// Devuelve Visible SOLO si la restauración es "Endodoncia".
    /// </summary>
    public class EndodonciaToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ToothRestoration restoration)
            {
                return restoration == ToothRestoration.Endodoncia ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}