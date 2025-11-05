using MahApps.Metro.Controls;
using System;
using System.Linq;
using System.Windows;
using TuClinica.Core.Enums;

namespace TuClinica.UI.Views
{
    public partial class OdontogramStateDialog : MetroWindow
    {
        // Propiedades públicas para que el ViewModel recoja el resultado
        public ToothCondition NewCondition { get; private set; }
        public ToothRestoration NewRestoration { get; private set; }

        public OdontogramStateDialog()
        {
            InitializeComponent();

            // Cargar los ComboBox con los valores de los Enums
            ConditionComboBox.ItemsSource = Enum.GetValues(typeof(ToothCondition)).Cast<ToothCondition>();
            RestorationComboBox.ItemsSource = Enum.GetValues(typeof(ToothRestoration)).Cast<ToothRestoration>();
        }

        /// <summary>
        /// Carga el diálogo con el estado actual de la superficie seleccionada.
        /// </summary>
        public void LoadState(int toothNumber, ToothSurface surface, ToothCondition currentCondition, ToothRestoration currentRestoration)
        {
            // Actualizar el título
            TitleTextBlock.Text = $"Editando: Diente {toothNumber} (Superficie: {surface})";

            // Seleccionar los valores actuales en los ComboBox
            ConditionComboBox.SelectedItem = currentCondition;
            RestorationComboBox.SelectedItem = currentRestoration;

            // Asignar por defecto en caso de que el usuario solo dé a Aceptar
            NewCondition = currentCondition;
            NewRestoration = currentRestoration;
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            // Guardar los valores seleccionados en las propiedades públicas
            if (ConditionComboBox.SelectedItem is ToothCondition condition)
            {
                NewCondition = condition;
            }
            if (RestorationComboBox.SelectedItem is ToothRestoration restoration)
            {
                NewRestoration = restoration;
            }

            this.DialogResult = true;
        }
    }
}