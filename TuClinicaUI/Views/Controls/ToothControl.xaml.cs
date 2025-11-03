using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System;
using System.Globalization;
using TuClinica.Core.Enums;

namespace TuClinica.UI.Views.Controls
{
    public partial class ToothControl : UserControl
    {
        public ToothControl()
        {
            InitializeComponent();
        }
    }

    /// <summary>
    /// Convierte un ToothStatus en un color para la UI.
    /// </summary>
    public class ToothStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ToothStatus status)
            {
                return status switch
                {
                    ToothStatus.Sano => Brushes.White,
                    ToothStatus.Caries => Brushes.Red,
                    ToothStatus.Obturacion => Brushes.Blue,
                    ToothStatus.ExtraccionIndicada => Brushes.Black,
                    ToothStatus.Ausente => Brushes.LightGray,
                    ToothStatus.Corona => Brushes.Gold,
                    ToothStatus.Implante => Brushes.Purple,
                    ToothStatus.Endodoncia => Brushes.Orange,
                    _ => Brushes.Transparent
                };
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}