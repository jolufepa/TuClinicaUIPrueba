using System.Windows.Controls;

// 1. El namespace debe ser "TuClinica.UI.Views"
namespace TuClinica.UI.Views
{
    /// <summary>
    /// Lógica de interacción para PatientsView.xaml
    /// </summary>

    // 2. ESTA ES LA LÍNEA CLAVE: Debe definir "PatientsView"
    //    y heredar de "UserControl"
    public partial class PatientsView : UserControl
    {
        public PatientsView()
        {
            InitializeComponent();
        }
    }
}
