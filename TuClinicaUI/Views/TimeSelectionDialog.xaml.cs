using MahApps.Metro.Controls;
using System;
using System.Windows;

namespace TuClinica.UI.Views
{
    public partial class TimeSelectionDialog : MetroWindow
    {
        public DateTime SelectedStart { get; private set; }
        public DateTime SelectedEnd { get; private set; }

        public TimeSelectionDialog()
        {
            InitializeComponent();
            // Por defecto: Entrada hace 1 hora, Salida ahora
            StartTimePicker.SelectedDateTime = DateTime.Now.AddHours(-1);
            EndTimePicker.SelectedDateTime = DateTime.Now;
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            if (StartTimePicker.SelectedDateTime == null || EndTimePicker.SelectedDateTime == null)
            {
                MessageBox.Show("Debe seleccionar ambas horas.", "Error");
                return;
            }
            if (StartTimePicker.SelectedDateTime > EndTimePicker.SelectedDateTime)
            {
                MessageBox.Show("La hora de entrada no puede ser posterior a la de salida.", "Error");
                return;
            }

            SelectedStart = StartTimePicker.SelectedDateTime.Value;
            SelectedEnd = EndTimePicker.SelectedDateTime.Value;
            DialogResult = true;
        }
    }
}