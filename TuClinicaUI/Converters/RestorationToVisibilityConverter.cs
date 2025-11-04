using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TuClinica.Core.Enums;

namespace TuClinica.UI.Converters
{
    /// <summary>
    /// Convierte ToothRestoration a Visibility. Visible si hay una restauración, Hidden si es Ninguna.
    /// </summary>
    public class RestorationToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ToothRestoration restoration)
            {
                // Solo es visible si es CUALQUIER COSA excepto Ninguna
                return restoration == ToothRestoration.Ninguna ? Visibility.Hidden : Visibility.Visible;
            }
            return Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}