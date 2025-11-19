using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TuClinica.UI.Converters
{
    /// <summary>
    /// Convierte el número de diente FDI en una posición (Margin) exacta dentro del Canvas del Odontograma.
    /// Optimizado para un lienzo base de 300x350 unidades.
    /// </summary>
    public class ToothNumberPositionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not int tooth) return new Thickness(0);

            Point p = GetPosition(tooth);
            return new Thickness(p.X, p.Y, 0, 0);
        }

        private Point GetPosition(int tooth)
        {
            return tooth switch
            {
                // --- CUADRANTE 1 (Superior Derecho) ---
                18 => new Point(0, 120),
                17 => new Point(10, 100),
                16 => new Point(22, 80),
                15 => new Point(35, 62),
                14 => new Point(50, 48),
                13 => new Point(68, 36),
                12 => new Point(86, 28),
                11 => new Point(104, 24),

                // --- CUADRANTE 2 (Superior Izquierdo) ---
                21 => new Point(126, 24),
                22 => new Point(144, 28),
                23 => new Point(162, 36),
                24 => new Point(180, 48),
                25 => new Point(195, 62),
                26 => new Point(208, 80),
                27 => new Point(220, 100),
                28 => new Point(230, 120),

                // --- CUADRANTE 4 (Inferior Derecho) ---
                48 => new Point(0, 170),
                47 => new Point(10, 190),
                46 => new Point(22, 210),
                45 => new Point(35, 228),
                44 => new Point(50, 242),
                43 => new Point(68, 254),
                42 => new Point(86, 262),
                41 => new Point(104, 266),

                // --- CUADRANTE 3 (Inferior Izquierdo) ---
                31 => new Point(126, 266),
                32 => new Point(144, 262),
                33 => new Point(162, 254),
                34 => new Point(180, 242),
                35 => new Point(195, 228),
                36 => new Point(208, 210),
                37 => new Point(220, 190),
                38 => new Point(230, 170),

                // Dientes temporales (Opcional/Futuro) - Se pueden añadir aquí
                // 51 => new Point(x, y)...

                _ => new Point(-100, -100) // Ocultar fuera de pantalla si no existe
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}