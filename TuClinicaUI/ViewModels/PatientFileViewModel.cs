using CommunityToolkit.Mvvm.ComponentModel;
using TuClinica.Core.Models;

namespace TuClinica.UI.ViewModels
{
    // Será la base para la vista de Ficha de Paciente completa
    public partial class PatientFileViewModel : BaseViewModel
    {
        [ObservableProperty]
        private Patient? _currentPatient;

        // Propiedad para la pestaña seleccionada (Datos Personales, Ficha Clínica, etc.)
        // (La usaremos en el futuro)

        // Método para que otros ViewModels carguen el paciente
        public void LoadPatient(Patient patient)
        {
            CurrentPatient = patient;
            // Aquí, en el futuro, también cargarías sus evoluciones, odontograma, etc.
        }
    }
}