using System.Windows.Controls;

// 1. El namespace debe ser "TuClinica.UI.Views"
namespace TuClinica.UI.Views
{
    /// <summary>
    /// Lógica de interacción para PatientFileView.xaml
    /// </summary>

    // 2. ESTA ES LA LÍNEA CLAVE: Debe heredar de "UserControl"
    //    Aquí es donde estaba el error.
    public partial class PatientFileView : UserControl
    {
        public PatientFileView()
        {
            InitializeComponent();
        }
    }
}