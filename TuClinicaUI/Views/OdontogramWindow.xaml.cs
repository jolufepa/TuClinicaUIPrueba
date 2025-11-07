using MahApps.Metro.Controls;
using System.Windows;
// --- USINGS AÑADIDOS ---
using System;
using System.ComponentModel;
using TuClinica.UI.ViewModels;
// -----------------------

namespace TuClinica.UI.Views
{
    public partial class OdontogramWindow : MetroWindow
    {
        // --- CÓDIGO AÑADIDO ---

        // Propiedad pública para acceder fácilmente al ViewModel
        public OdontogramViewModel? ViewModel => DataContext as OdontogramViewModel;

        public OdontogramWindow()
        {
            InitializeComponent();

            // Escuchamos el evento DataContextChanged
            DataContextChanged += OdontogramWindow_DataContextChanged;
        }

        private void OdontogramWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Desuscribir del ViewModel anterior si existe
            if (e.OldValue is OdontogramViewModel oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }

            // Suscribir al nuevo ViewModel si existe
            if (e.NewValue is OdontogramViewModel newVm)
            {
                newVm.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        // Este método se llama cuando una propiedad del ViewModel cambia
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Si la propiedad que cambió es "DialogResult"
            if (e.PropertyName == nameof(ViewModel.DialogResult))
            {
                if (ViewModel?.DialogResult != null)
                {
                    // Asignamos ese valor (true o false) al DialogResult de la ventana,
                    // lo que la cierra automáticamente.
                    try
                    {
                        this.DialogResult = ViewModel.DialogResult;
                    }
                    catch (InvalidOperationException)
                    {
                        // Evita un crash si se intenta cerrar antes de mostrarse
                        this.Close();
                    }
                }
            }
        }

        // Es buena práctica desuscribirse al cerrar
        protected override void OnClosed(EventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
            DataContextChanged -= OdontogramWindow_DataContextChanged;
            base.OnClosed(e);
        }

        // --- FIN DE CÓDIGO AÑADIDO ---
    }
}