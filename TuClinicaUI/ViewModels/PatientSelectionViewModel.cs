using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Models;
using System.ComponentModel; // Para PropertyChangedEventArgs
using System.Windows.Input; // Para ICommand

// IMPORTANTE: Quitar 'partial'
namespace TuClinica.UI.ViewModels
{
    // NO hereda de ObservableObject/BaseViewModel si no usas CommunityToolkit,
    // pero lo mantendremos para usar SetProperty
    public class PatientSelectionViewModel : BaseViewModel
    {
        private readonly IPatientRepository _patientRepository;

        // --- Propiedades Manuales ---

        // Texto introducido en el buscador
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    // Llama a la lógica de búsqueda al cambiar el texto
                    _ = SearchAsync();
                }
            }
        }

        // Resultados de la búsqueda para mostrar en la lista
        private ObservableCollection<Patient> _searchResults = new ObservableCollection<Patient>();
        public ObservableCollection<Patient> SearchResults
        {
            get => _searchResults;
            set => SetProperty(ref _searchResults, value);
        }

        // Paciente seleccionado en la lista de resultados
        private Patient? _selectedPatientFromList;
        public Patient? SelectedPatientFromList
        {
            get => _selectedPatientFromList;
            set
            {
                if (SetProperty(ref _selectedPatientFromList, value))
                {
                    // Notifica al comando
                    ConfirmSelectionCommand.NotifyCanExecuteChanged();
                    // Y notifica a la propiedad auxiliar
                    OnPropertyChanged(nameof(IsPatientSelected));
                }
            }
        }

        // Propiedad para indicar si se confirmó la selección (para el diálogo)
        private bool _dialogResult = false;
        public bool DialogResult
        {
            get => _dialogResult;
            // Solo necesitamos que notifique, el set es privado
            set => SetProperty(ref _dialogResult, value);
        }

        // --- Comandos Manuales ---
        public IAsyncRelayCommand SearchCommand { get; }
        public IRelayCommand ConfirmSelectionCommand { get; }

        // Propiedad auxiliar para habilitar el botón Confirmar
        public bool IsPatientSelected => SelectedPatientFromList != null;


        public PatientSelectionViewModel(IPatientRepository patientRepository)
        {
            _patientRepository = patientRepository;

            // Inicialización de comandos
            SearchCommand = new AsyncRelayCommand(SearchAsync);
            ConfirmSelectionCommand = new RelayCommand(ConfirmSelection, () => IsPatientSelected);
        }

        // Método que ejecuta el comando SearchCommand
        private async Task SearchAsync()
        {
            SearchResults.Clear();
            SelectedPatientFromList = null;

            if (!string.IsNullOrWhiteSpace(SearchText) && SearchText.Length > 1)
            {
                var results = await _patientRepository.SearchByNameOrDniAsync(SearchText);
                if (results != null)
                {
                    // No necesitas usar AddRangeAsync aquí, solo añadir a la ObservableCollection
                    foreach (var patient in results)
                    {
                        SearchResults.Add(patient);
                    }
                }
            }
        }

        // Método que ejecuta el comando ConfirmSelectionCommand
        private void ConfirmSelection()
        {
            if (SelectedPatientFromList != null)
            {
                // Establecemos la propiedad DialogResult a true.
                DialogResult = true;
            }
        }
    }
}