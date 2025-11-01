using System.ComponentModel; // Para PropertyChangedEventArgs
using System.Windows;
using TuClinica.UI.ViewModels; // Para UserEditViewModel

namespace TuClinica.UI.Views
{
    /// <summary>
    /// Lógica de interacción para UserEditDialog.xaml
    /// </summary>
    public partial class UserEditDialog : Window // Asegúrate que hereda de Window o MahApps.Metro.Controls.MetroWindow si la usas
    {
        public UserEditDialog()
        {
            InitializeComponent();
            // Escuchamos el evento DataContextChanged para suscribirnos a los cambios del ViewModel
            DataContextChanged += UserEditDialog_DataContextChanged;
        }

        private void UserEditDialog_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Desuscribir del ViewModel anterior si existe
            if (e.OldValue is UserEditViewModel oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }

            // Suscribir al nuevo ViewModel si existe
            if (e.NewValue is UserEditViewModel newVm)
            {
                newVm.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        // Este método se llama cuando una propiedad del ViewModel cambia
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Si la propiedad que cambió es DialogResult...
            if (e.PropertyName == nameof(UserEditViewModel.DialogResult))
            {
                var viewModel = DataContext as UserEditViewModel;
                if (viewModel?.DialogResult != null)
                {
                    // ...asignamos ese valor al DialogResult de la ventana, lo que la cierra.
                    try
                    {
                        this.DialogResult = viewModel.DialogResult;
                    }
                    catch (InvalidOperationException)
                    {
                        // Esto puede pasar si intentas poner DialogResult ANTES
                        // de que la ventana se muestre como diálogo. Lo ignoramos.
                        // Alternativamente, podríamos cerrar con this.Close() aquí.
                    }
                }
            }
        }
    }
}