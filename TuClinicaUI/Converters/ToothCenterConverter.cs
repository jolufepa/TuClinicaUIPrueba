using System;
using System.Collections.Generic; // Necesario para HashSet
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TuClinica.UI.Converters
{
    /// <summary>
    /// Calcula el centro geométrico de la cara Oclusal (o Lingual en dientes anteriores) 
    /// para posicionar la Endodoncia.
    /// Devuelve un TranslateTransform que mueve el punto al lugar exacto.
    /// </summary>
    public class ToothCenterConverter : IValueConverter
    {
        // Lista de dientes anteriores (Incisivos y Caninos) que no tienen cara Oclusal en el dibujo,
        // por lo que usaremos la cara Lingual para centrar el punto.
        private static readonly HashSet<int> AnteriorTeeth = new HashSet<int>
        {
            11, 12, 13,
            21, 22, 23,
            31, 32, 33,
            41, 42, 43
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // value es el ToothNumber (int)
            // parameter es el tamaño del círculo (double) para centrarlo perfectamente (offset)

            if (value is int toothNumber)
            {
                // Lógica corregida: 
                // Si es diente anterior -> Busca "Lingual". 
                // Si es posterior -> Busca "Oclusal".
                string surfaceName = AnteriorTeeth.Contains(toothNumber) ? "Lingual" : "Oclusal";
                string resourceKey = $"Geo_{toothNumber}_{surfaceName}";

                double circleSize = 0;

                if (parameter != null && double.TryParse(parameter.ToString(), NumberStyles.Any, culture, out double size))
                {
                    circleSize = size;
                }

                try
                {
                    // Buscamos la geometría real en los recursos
                    if (Application.Current.Resources.Contains(resourceKey))
                    {
                        var geometry = Application.Current.Resources[resourceKey] as Geometry;
                        if (geometry != null)
                        {
                            // --- AQUÍ ESTÁ LA MAGIA ---
                            // Obtenemos los límites rectangulares del dibujo del diente
                            Rect bounds = geometry.Bounds;

                            // Calculamos el centro exacto de la geometría encontrada
                            double centerX = bounds.X + (bounds.Width / 2);
                            double centerY = bounds.Y + (bounds.Height / 2);

                            // Ajustamos para que el centro del círculo coincida con el centro del diente
                            // (Restamos la mitad del tamaño del círculo)
                            double finalX = centerX - (circleSize / 2);
                            double finalY = centerY - (circleSize / 2);

                            return new TranslateTransform(finalX, finalY);
                        }
                    }
                }
                catch
                {
                    // Si falla, no mover (se quedará en 0,0 pero oculto por defecto)
                }
            }

            return new TranslateTransform(0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}