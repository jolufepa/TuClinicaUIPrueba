using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TuClinica.UI.Converters
{
    public class ToothNumberToGeometryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int toothNumber && parameter is string surfaceName)
            {
                // --- LÓGICA DE MAPEO (ESPEJO) ---
                // Solo tenemos dibujos para el Cuadrante 1 (1x) y 4 (4x).
                // Para el 2 y el 3, usamos los del 1 y 4 respectivamente.

                int targetToothNumber = toothNumber;

                if (toothNumber >= 21 && toothNumber <= 28)
                {
                    // Cuadrante 2 usa geometrías del Cuadrante 1
                    // Ej: 21 -> 11, 28 -> 18
                    targetToothNumber = toothNumber - 10;
                }
                else if (toothNumber >= 31 && toothNumber <= 38)
                {
                    // Cuadrante 3 usa geometrías del Cuadrante 4
                    // Ej: 31 -> 41, 38 -> 48
                    targetToothNumber = toothNumber + 10;
                }

                // Construimos la clave (Ej: "Geo_11_Vestibular")
                string resourceKey = $"Geo_{targetToothNumber}_{surfaceName}";

                try
                {
                    if (Application.Current.Resources.Contains(resourceKey))
                    {
                        return Application.Current.Resources[resourceKey] as Geometry ?? Geometry.Empty;
                    }
                }
                catch
                {
                    return Geometry.Empty;
                }
            }

            return Geometry.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}