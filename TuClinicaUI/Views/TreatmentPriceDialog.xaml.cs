using MahApps.Metro.Controls;
using System;
using System.Linq;
using System.Windows;
using TuClinica.Core.Enums;

namespace TuClinica.UI.Views
{
    public partial class TreatmentPriceDialog : MetroWindow
    {
        public ToothStatus? SelectedStatus { get; private set; }
        public decimal Price { get; private set; }

        public TreatmentPriceDialog()
        {
            InitializeComponent();

            // Cargar el ComboBox con los valores del enum ToothStatus
            TreatmentComboBox.ItemsSource = Enum.GetValues(typeof(ToothStatus))
                                                .Cast<ToothStatus>()
                                                .ToList();
            TreatmentComboBox.SelectedIndex = 0;
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            if (TreatmentComboBox.SelectedItem == null)
            {
                MessageBox.Show("Debe seleccionar un tratamiento.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedStatus = (ToothStatus)TreatmentComboBox.SelectedItem;
            Price = (decimal)PriceNumericUpDown.Value;

            this.DialogResult = true;
        }
    }
}
