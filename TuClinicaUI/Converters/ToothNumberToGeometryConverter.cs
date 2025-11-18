using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TuClinica.UI.Converters
{
    /// <summary>
    /// Busca dinámicamente un recurso de Geometría (StreamGeometry) basado en el número de diente y la superficie.
    /// Espera recursos con el formato de nombre: "Geo_{ToothNumber}_{Surface}" (ej: "Geo_18_Vestibular").
    /// </summary>
    public class ToothNumberToGeometryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // value: El número de diente (int)
            // parameter: El nombre de la superficie (string) enviado desde el XAML (ej: "Oclusal", "Vestibular")

            if (value is int toothNumber && parameter is string surfaceName)
            {
                // Construimos la clave del recurso exacta. 
                // Esto permite que cada diente tenga su propia anatomía única si el SVG lo provee.
                string resourceKey = $"Geo_{toothNumber}_{surfaceName}";

                try
                {
                    // Intentamos buscar el recurso en el diccionario de la aplicación
                    if (Application.Current.Resources.Contains(resourceKey))
                    {
                        var geometry = Application.Current.Resources[resourceKey] as Geometry;
                        return geometry ?? Geometry.Empty;
                    }

                    // FALLBACK (Opcional): Lógica de espejo si no quieres dibujar los 32 dientes.
                    // Si no encuentra "Geo_21_...", intenta buscar el del cuadrante 1 "Geo_11_..." y aplicar transformación.
                    // Por ahora, asumiremos un odontograma profesional completo (32 definiciones).
                }
                catch
                {
                    // Si algo falla, no rompemos la UI, devolvemos vacío.
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