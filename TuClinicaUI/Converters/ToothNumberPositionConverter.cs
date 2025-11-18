using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TuClinica.UI.Converters
{
    /// <summary>
    /// Devuelve coordenadas FIJAS y PRECISAS para cada número de diente,
    /// creando un arco limpio alrededor del odontograma.
    /// </summary>
    public class ToothNumberPositionConverter : IValueConverter
    {
        // Mapeo manual de coordenadas (X, Y) para cada diente.
        // Ajustado para un lienzo de aprox 200x300.
        private static readonly Dictionary<int, Point> _coordinates = new Dictionary<int, Point>
        {
            // === CUADRANTE 1 (Superior Derecho - Lado Izquierdo Pantalla) ===
            { 18, new Point(0, 125) },
            { 17, new Point(10, 105) },
            { 16, new Point(20, 85) },
            { 15, new Point(32, 68) },
            { 14, new Point(45, 52) },
            { 13, new Point(60, 38) },
            { 12, new Point(75, 28) },
            { 11, new Point(90, 22) },

            // === CUADRANTE 2 (Superior Izquierdo - Lado Derecho Pantalla) ===
            { 21, new Point(108, 22) },
            { 22, new Point(123, 28) },
            { 23, new Point(138, 38) },
            { 24, new Point(153, 52) },
            { 25, new Point(166, 68) },
            { 26, new Point(178, 85) },
            { 27, new Point(188, 105) },
            { 28, new Point(198, 125) },

            // === CUADRANTE 3 (Inferior Izquierdo - Lado Derecho Pantalla) ===
            { 38, new Point(198, 165) },
            { 37, new Point(188, 185) },
            { 36, new Point(178, 205) },
            { 35, new Point(166, 222) },
            { 34, new Point(153, 238) },
            { 33, new Point(138, 252) },
            { 32, new Point(123, 262) },
            { 31, new Point(108, 268) },

            // === CUADRANTE 4 (Inferior Derecho - Lado Izquierdo Pantalla) ===
            { 48, new Point(0, 165) },
            { 47, new Point(10, 185) },
            { 46, new Point(20, 205) },
            { 45, new Point(32, 222) },
            { 44, new Point(45, 238) },
            { 43, new Point(60, 252) },
            { 42, new Point(75, 262) },
            { 41, new Point(90, 268) },

            // === TEMPORALES (Ajustados dentro del arco principal o fuera según preferencia) ===
            // Superior
            { 55, new Point(45, 75) }, { 54, new Point(55, 65) }, { 53, new Point(65, 58) }, { 52, new Point(78, 52) }, { 51, new Point(90, 50) },
            { 61, new Point(108, 50) }, { 62, new Point(120, 52) }, { 63, new Point(133, 58) }, { 64, new Point(143, 65) }, { 65, new Point(153, 75) },
            // Inferior
            { 85, new Point(45, 215) }, { 84, new Point(55, 225) }, { 83, new Point(65, 232) }, { 82, new Point(78, 238) }, { 81, new Point(90, 240) },
            { 71, new Point(108, 240) }, { 72, new Point(120, 238) }, { 73, new Point(133, 232) }, { 74, new Point(143, 225) }, { 75, new Point(153, 215) }
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int toothNumber && _coordinates.ContainsKey(toothNumber))
            {
                Point p = _coordinates[toothNumber];
                // Devolvemos un Margin. Como el Grid no tiene tamaño definido, 
                // esto posicionará el elemento relativo a la esquina superior izquierda (0,0) del contenedor.
                return new Thickness(p.X, p.Y, 0, 0);
            }
            // Si no está en la lista, lo mandamos lejos para que no estorbe
            return new Thickness(-100, -100, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}