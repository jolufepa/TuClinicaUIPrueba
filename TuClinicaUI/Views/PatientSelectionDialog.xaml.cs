using MahApps.Metro.Controls;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using TuClinica.UI.ViewModels;

namespace TuClinica.UI.Views
{
    public partial class PatientSelectionDialog : MetroWindow
    {
        // Propiedad pública para obtener el ViewModel (y su resultado) desde fuera
        public PatientSelectionViewModel ViewModel => (PatientSelectionViewModel)DataContext;

        public PatientSelectionDialog()
        {
            InitializeComponent();

            // Obtenemos y asignamos el ViewModel
            DataContext = App.AppHost!.Services.GetRequiredService<PatientSelectionViewModel>();

            // Nos suscribimos al evento PropertyChanged del ViewModel
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        // Método que se ejecuta cuando una propiedad del ViewModel cambia
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Si la propiedad que cambió fue DialogResult y es true...
            if (e.PropertyName == nameof(ViewModel.DialogResult) && ViewModel.DialogResult)
            {
                // ...establecemos el DialogResult de la ventana a true y la cerramos
                this.DialogResult = true;
                //this.Close(); // Cierra la ventana
            }
        }

        // Es buena práctica desuscribirse del evento al cerrar para evitar fugas de memoria
        protected override void OnClosed(EventArgs e)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            base.OnClosed(e);
        }
    }
}
