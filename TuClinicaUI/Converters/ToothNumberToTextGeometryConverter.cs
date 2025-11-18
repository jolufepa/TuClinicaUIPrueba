using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TuClinica.UI.Converters
{
    /// <summary>
    /// Busca el recurso vectorial del NÚMERO del diente (ej: "Num_12").
    /// Esto permite usar dibujos exactos de Inkscape para los textos.
    /// </summary>
    public class ToothNumberToTextGeometryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int toothNumber)
            {
                // Busca un recurso llamado "Num_11", "Num_12", etc.
                string resourceKey = $"Num_{toothNumber}";

                try
                {
                    if (Application.Current.Resources.Contains(resourceKey))
                    {
                        return Application.Current.Resources[resourceKey] as Geometry ?? Geometry.Empty;
                    }
                }
                catch { }
            }
            return Geometry.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}