using MahApps.Metro.Controls;
using System.Windows;
// --- AÑADIR ESTE USING ---
using System;

namespace TuClinica.UI.Views
{
    public partial class NewPaymentDialog : MetroWindow
    {
        public decimal Amount { get; private set; }
        public string PaymentMethod { get; private set; } = string.Empty;

        // --- PROPIEDADES NUEVAS ---
        public DateTime? SelectedDate { get; private set; }
        public string Observaciones { get; private set; } = string.Empty;
        // --- FIN PROPIEDADES NUEVAS ---

        public NewPaymentDialog()
        {
            InitializeComponent();
            MethodComboBox.SelectedIndex = 0; // Por defecto "Efectivo"
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            if (AmountNumericUpDown.Value == null || AmountNumericUpDown.Value <= 0)
            {
                MessageBox.Show("El monto debe ser mayor que cero.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(MethodComboBox.Text))
            {
                MessageBox.Show("Debe seleccionar o introducir un método de pago.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Amount = (decimal)AmountNumericUpDown.Value;
            PaymentMethod = MethodComboBox.Text;

            // --- LEER NUEVOS VALORES ---
            Observaciones = ObservacionesTextBox.Text;
            SelectedDate = DatePickerControl.SelectedDate;
            // --- FIN LEER NUEVOS VALORES ---

            this.DialogResult = true;
        }
    }
}