using System.Windows;
using System.Windows.Controls;
using TuClinica.UI.ViewModels.Events; // <-- MODIFICADO: Apunta a la subcarpeta

namespace TuClinica.UI.Selectors
{
    public class HistorialTemplateSelector : DataTemplateSelector
    {
        // Añade el ? después de DataTemplate
        public DataTemplate? CargoTemplate { get; set; }
        public DataTemplate? AbonoTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is CargoEvent && CargoTemplate != null) // Añade chequeo de null
            {
                return CargoTemplate;
            }

            if (item is AbonoEvent && AbonoTemplate != null) // Añade chequeo de null
            {
                return AbonoTemplate;
            }

            return base.SelectTemplate(item, container);
        }
    }
}