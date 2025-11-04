using MahApps.Metro.Controls;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TuClinica.Core.Enums;
using TuClinica.Core.Models;
using System.Collections.Generic;
using System.Globalization;

namespace TuClinica.UI.Views
{
    public partial class TreatmentPriceDialog : MetroWindow
    {
        public ToothRestoration? SelectedRestoration { get; set; }
        public decimal Price { get; private set; }
        public int? SelectedTreatmentId { get; set; }

        public IEnumerable<Treatment> AvailableTreatments
        {
            get => (IEnumerable<Treatment>)TreatmentComboBox.ItemsSource;
            set => TreatmentComboBox.ItemsSource = value;
        }

        public TreatmentPriceDialog()
        {
            InitializeComponent();

            // Inicializar ComboBox de restauración
            RestorationComboBox.ItemsSource = Enum.GetValues(typeof(ToothRestoration))
                                                 .Cast<ToothRestoration>()
                                                 .Where(r => r != ToothRestoration.Ninguna)
                                                 .ToList();
            RestorationComboBox.SelectedIndex = 0;

            // Valor inicial seguro
            PriceNumericUpDown.Value = 0.0; // double

            // Suscripción al evento
            TreatmentComboBox.SelectionChanged += TreatmentComboBox_SelectionChanged;
        }

        private void TreatmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TreatmentComboBox.SelectedItem is Treatment selectedTreatment)
            {
                SelectedTreatmentId = selectedTreatment.Id;
                // CORRECCIÓN: decimal → double
                PriceNumericUpDown.Value = (double)selectedTreatment.DefaultPrice;
            }
            else
            {
                SelectedTreatmentId = null;
                PriceNumericUpDown.Value = 0.0;
            }
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validar tratamiento
            if (TreatmentComboBox.SelectedItem is not Treatment finalTreatment)
            {
                MessageBox.Show("Debe seleccionar un Tratamiento de la lista.", "Error de Selección",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Validar restauración
            if (RestorationComboBox.SelectedItem == null)
            {
                MessageBox.Show("Debe seleccionar el tipo de restauración/estado.", "Error de Selección",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3. Validar precio (double? → decimal)
            if (!PriceNumericUpDown.Value.HasValue || PriceNumericUpDown.Value < 0)
            {
                MessageBox.Show("El precio debe ser un número válido mayor o igual a cero.", "Error de Precio",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal priceRead = (decimal)PriceNumericUpDown.Value.Value;

            // Asignar resultados
            SelectedTreatmentId = finalTreatment.Id;
            SelectedRestoration = (ToothRestoration)RestorationComboBox.SelectedItem;
            Price = priceRead;

            this.DialogResult = true;
        }
    }
}