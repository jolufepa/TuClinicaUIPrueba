// En: TuClinicaUI/Views/LinkedDocumentDialog.xaml.cs
using MahApps.Metro.Controls;
using System.Windows;
using TuClinica.Core.Enums;

namespace TuClinica.UI.Views
{
    public partial class LinkedDocumentDialog : MetroWindow
    {
        // Propiedades públicas para que el DialogService recoja los datos
        public PatientDocumentType DocumentType { get; private set; }
        public string DocumentNumber { get; private set; } = string.Empty;
        public string Notes { get; private set; } = string.Empty;

        public LinkedDocumentDialog()
        {
            InitializeComponent();
            // Seleccionar DNI por defecto
            DocumentTypeComboBox.SelectedItem = PatientDocumentType.DNI;
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validar que el número no esté vacío
            if (string.IsNullOrWhiteSpace(DocumentNumberTextBox.Text))
            {
                MessageBox.Show("El número de documento no puede estar vacío.", "Dato Requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Guardar los valores en las propiedades públicas
            DocumentType = (PatientDocumentType)DocumentTypeComboBox.SelectedItem;
            DocumentNumber = DocumentNumberTextBox.Text.ToUpper().Trim();
            Notes = NotesTextBox.Text.Trim();

            // 3. Cerrar el diálogo
            this.DialogResult = true;
        }
    }
}