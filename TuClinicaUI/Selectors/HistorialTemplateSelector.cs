using System.Windows;
using System.Windows.Controls;
using TuClinica.UI.ViewModels.Events; // <-- MODIFICADO: Apunta a la subcarpeta

namespace TuClinica.UI.Selectors
{
    public class HistorialTemplateSelector : DataTemplateSelector
    {
        // Estas propiedades se enlazarán en el XAML
        public DataTemplate CargoTemplate { get; set; }
        public DataTemplate AbonoTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            // Apuntan a los tipos correctos (sin "PatientFileViewModel.")
            if (item is CargoEvent)
            {
                return CargoTemplate;
            }

            if (item is AbonoEvent)
            {
                return AbonoTemplate;
            }

            return base.SelectTemplate(item, container);
        }
    }
}