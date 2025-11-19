using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TuClinica.UI.Converters
{
    /// <summary>
    /// Genera una curva que conecta los centros EXACTOS de las geometrías vestibulares.
    /// CORREGIDO: Usa las coordenadas absolutas de los recursos, sin offsets manuales.
    /// </summary>
    public class ToothListToPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not List<int> teethSequence || teethSequence.Count < 2)
                return Geometry.Empty;

            PathFigure figure = new PathFigure();
            bool isFirst = true;

            foreach (int toothNum in teethSequence)
            {
                // 1. Obtenemos el punto central REAL de la geometría
                Point? centerPoint = GetAbsoluteVestibularCenter(toothNum);

                // Si no existe geometría para ese diente, saltamos
                if (centerPoint == null) continue;

                Point p = centerPoint.Value;

                if (isFirst)
                {
                    figure.StartPoint = p;
                    isFirst = false;
                }
                else
                {
                    // PolyLineSegment crea una conexión recta al siguiente punto.
                    // Al tener muchos puntos (diente a diente), forma el arco.
                    figure.Segments.Add(new LineSegment(p, true));
                }
            }

            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

        private Point? GetAbsoluteVestibularCenter(int toothNumber)
        {
            string resourceKey = $"Geo_{toothNumber}_Vestibular";

            try
            {
                if (Application.Current.Resources.Contains(resourceKey))
                {
                    if (Application.Current.Resources[resourceKey] is Geometry geo)
                    {
                        // ¡AQUÍ ESTÁ LA CLAVE!
                        // Las geometrías en ToothGeometries.xaml ya tienen coordenadas absolutas (ej: 150, 200).
                        // Solo necesitamos el centro de su caja contenedora.
                        Rect bounds = geo.Bounds;

                        // Calculamos el centro exacto del dibujo vestibular
                        double centerX = bounds.X + (bounds.Width / 2);
                        double centerY = bounds.Y + (bounds.Height / 2);

                        return new Point(centerX, centerY);
                    }
                }
            }
            catch
            {
                // Si falla la búsqueda del recurso, ignoramos este punto
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}