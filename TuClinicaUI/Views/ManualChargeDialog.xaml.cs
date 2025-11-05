using MahApps.Metro.Controls;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using TuClinica.Core.Models;

namespace TuClinica.UI.Views
{
    public partial class ManualChargeDialog : MetroWindow
    {
        // Propiedades públicas para que el ViewModel recoja el resultado
        public Treatment? SelectedTreatment { get; private set; }
        public string ManualConcept { get; private set; } = string.Empty;
        public decimal UnitPrice { get; private set; } // Propiedad que falta
        public int Quantity { get; private set; } // Propiedad que falta

        // Propiedad para cargar los tratamientos desde el ViewModel
        public IEnumerable<Treatment> AvailableTreatments
        {
            get => (IEnumerable<Treatment>)TreatmentComboBox.ItemsSource;
            set => TreatmentComboBox.ItemsSource = value;
        }

        public ManualChargeDialog()
        {
            InitializeComponent();
            PriceNumericUpDown.Value = 0.0;
            QuantityNumericUpDown.Value = 1; // Asegurarse de que el campo Cantidad (añadido en Fase 4) tiene valor
        }

        private void TreatmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Si el usuario selecciona un tratamiento, auto-rellenamos los campos
            if (TreatmentComboBox.SelectedItem is Treatment selectedTreatment)
            {
                SelectedTreatment = selectedTreatment;
                ConceptTextBox.Text = selectedTreatment.Name;
                PriceNumericUpDown.Value = (double)selectedTreatment.DefaultPrice;
                QuantityNumericUpDown.Value = 1; // Resetear a 1
            }
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            // Validación
            if (string.IsNullOrWhiteSpace(ConceptTextBox.Text))
            {
                MessageBox.Show("Debe introducir un Concepto o Diagnóstico.", "Dato Requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (PriceNumericUpDown.Value == null || PriceNumericUpDown.Value < 0)
            {
                MessageBox.Show("Debe introducir un Precio válido (0 o mayor).", "Dato Requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (QuantityNumericUpDown.Value == null || QuantityNumericUpDown.Value <= 0)
            {
                MessageBox.Show("La cantidad debe ser 1 o mayor.", "Dato Requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Guardar valores en las propiedades públicas
            ManualConcept = ConceptTextBox.Text;
            UnitPrice = (decimal)PriceNumericUpDown.Value.Value;
            Quantity = (int)QuantityNumericUpDown.Value.Value;

            this.DialogResult = true;
        }
    }
}