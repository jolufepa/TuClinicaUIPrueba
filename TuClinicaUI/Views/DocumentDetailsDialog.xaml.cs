using MahApps.Metro.Controls;
using System.Windows;
using TuClinica.Core.Enums;

namespace TuClinica.UI.Views
{
    public partial class DocumentDetailsDialog : MetroWindow
    {
        // Propiedades de salida
        public string ResultFileName { get; private set; } = string.Empty;
        public FileCategory ResultCategory { get; private set; }

        public DocumentDetailsDialog(string defaultFileName)
        {
            InitializeComponent();

            // Pre-rellenar datos
            FileNameTextBox.Text = defaultFileName;
            CategoryComboBox.SelectedItem = FileCategory.Otro; // Default

            // Foco en el nombre para editar rápido
            Loaded += (s, e) => FileNameTextBox.Focus();
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FileNameTextBox.Text))
            {
                MessageBox.Show("El nombre del archivo no puede estar vacío.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CategoryComboBox.SelectedItem == null)
            {
                MessageBox.Show("Debe seleccionar una categoría.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ResultFileName = FileNameTextBox.Text.Trim();
            ResultCategory = (FileCategory)CategoryComboBox.SelectedItem;

            DialogResult = true;
        }
    }
}