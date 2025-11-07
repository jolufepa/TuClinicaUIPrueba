using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TuClinica.Core.Enums;

namespace TuClinica.UI.Converters
{
    /// <summary>
    /// Convierte ToothRestoration a Visibility (Inverso).
    /// Muestra el elemento si la restauración es "Ninguna".
    /// Oculta el elemento si hay CUALQUIER restauración completa (Implante, Corona, etc.).
    /// </summary>
    public class RestorationToVisibilityInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ToothRestoration restoration)
            {
                // Visible si es "Ninguna", Colapsado si es cualquier otra cosa.
                return restoration == ToothRestoration.Ninguna ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}