using System.Windows;

namespace TuClinica.UI.Views
{
    /// <summary>
    /// Interaction logic for PasswordPromptDialog.xaml
    /// </summary>
    public partial class PasswordPromptDialog : Window // O MahApps.Metro.Controls.MetroWindow si usas esa base
    {
        // Propiedad pública para obtener la contraseña introducida
        public string Password => PasswordBoxInput.Password;

        public PasswordPromptDialog()
        {
            InitializeComponent();
            // Poner el foco en el campo de contraseña al abrir
            Loaded += (sender, e) => PasswordBoxInput.Focus();
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            // Validar que se introdujo algo (opcional, pero recomendado)
            if (string.IsNullOrWhiteSpace(PasswordBoxInput.Password))
            {
                MessageBox.Show("La contraseña no puede estar vacía.", "Contraseña Requerida", MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBoxInput.Focus();
                return; // No cerrar si está vacía
            }
            // Establecer DialogResult a true y cerrar
            this.DialogResult = true;
        }

        // El botón Cancelar cierra automáticamente porque IsCancel="True" en el XAML
    }
}