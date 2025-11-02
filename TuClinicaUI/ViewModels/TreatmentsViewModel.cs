using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Models;
using System.Windows;
using System.ComponentModel;
using System.Windows.Input;
using TuClinica.Core.Interfaces.Services; 
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;

// REMOVE 'partial'
namespace TuClinica.UI.ViewModels
{
    public class TreatmentsViewModel : BaseViewModel
    {
        private readonly ITreatmentRepository _treatmentRepository;
        private readonly IDialogService _dialogService;

        // --- Propiedades Manuales ---
        private ObservableCollection<Treatment> _treatments = new ObservableCollection<Treatment>();
        public ObservableCollection<Treatment> Treatments
        {
            get => _treatments;
            set => SetProperty(ref _treatments, value);
        }

        private Treatment? _selectedTreatment;
        public Treatment? SelectedTreatment
        {
            get => _selectedTreatment;
            set
            {
                if (SetProperty(ref _selectedTreatment, value))
                {
                    OnSelectedTreatmentChanged(value); // Llama al handler manual
                    OnPropertyChanged(nameof(IsTreatmentSelected));
                }
            }
        }

        private Treatment _treatmentFormModel = new Treatment { IsActive = true };
        public Treatment TreatmentFormModel
        {
            get => _treatmentFormModel;
            set => SetProperty(ref _treatmentFormModel, value);
        }

        private bool _isFormEnabled = false;
        public bool IsFormEnabled
        {
            get => _isFormEnabled;
            set => SetProperty(ref _isFormEnabled, value);
        }

        public bool IsTreatmentSelected => SelectedTreatment != null;

        // --- Comandos Manuales ---
        public IAsyncRelayCommand LoadTreatmentsCommand { get; }
        public IRelayCommand SetNewTreatmentFormCommand { get; }
        public IRelayCommand EditTreatmentCommand { get; }
        public IAsyncRelayCommand SaveTreatmentCommand { get; }
        public IAsyncRelayCommand DeleteTreatmentCommand { get; }


        public TreatmentsViewModel(ITreatmentRepository treatmentRepository, IDialogService dialogService)
        {
            _treatmentRepository = treatmentRepository;
            _dialogService = dialogService;

            // Inicialización de comandos
            LoadTreatmentsCommand = new AsyncRelayCommand(LoadTreatmentsAsync);
            SetNewTreatmentFormCommand = new RelayCommand(SetNewTreatmentForm);
            EditTreatmentCommand = new RelayCommand(EditTreatment, () => IsTreatmentSelected);
            SaveTreatmentCommand = new AsyncRelayCommand(SaveTreatmentAsync);
            DeleteTreatmentCommand = new AsyncRelayCommand(DeleteTreatmentAsync, () => IsTreatmentSelected);

            _ = LoadTreatmentsAsync();
        }

        // --- MÉTODOS DE COMANDOS ---

        private async Task LoadTreatmentsAsync()
        {
            var treatmentsFromDb = await _treatmentRepository.GetAllAsync();
            Treatments.Clear();
            if (treatmentsFromDb != null)
            {
                foreach (var treatment in treatmentsFromDb)
                {
                    Treatments.Add(treatment);
                }
            }
        }

        private void SetNewTreatmentForm()
        {
            TreatmentFormModel = new Treatment { IsActive = true };
            IsFormEnabled = true;
            SelectedTreatment = null;
        }

        private void EditTreatment()
        {
            if (SelectedTreatment == null) return;
            TreatmentFormModel = new Treatment
            {
                Id = SelectedTreatment.Id,
                Name = SelectedTreatment.Name,
                Description = SelectedTreatment.Description,
                DefaultPrice = SelectedTreatment.DefaultPrice,
                IsActive = SelectedTreatment.IsActive
            };
            IsFormEnabled = true;
        }

        private async Task SaveTreatmentAsync()
        {
            if (string.IsNullOrWhiteSpace(TreatmentFormModel.Name))
            {
                _dialogService.ShowMessage("El nombre del tratamiento no puede estar vacío.", "Error");
                return;
            }
            if (TreatmentFormModel.DefaultPrice < 0)
            {
                _dialogService.ShowMessage("El precio no puede ser negativo.", "Error");
                return;
            }


            if (TreatmentFormModel.Id == 0)
            {
                await _treatmentRepository.AddAsync(TreatmentFormModel);
            }
            else
            {
                var existingTreatment = await _treatmentRepository.GetByIdAsync(TreatmentFormModel.Id);
                if (existingTreatment != null)
                {
                    existingTreatment.Name = TreatmentFormModel.Name;
                    existingTreatment.Description = TreatmentFormModel.Description;
                    existingTreatment.DefaultPrice = TreatmentFormModel.DefaultPrice;
                    existingTreatment.IsActive = TreatmentFormModel.IsActive;
                }
                else { return; }
            }

            await _treatmentRepository.SaveChangesAsync();
            await LoadTreatmentsAsync();
            TreatmentFormModel = new Treatment { IsActive = true };
            IsFormEnabled = false;
        }

        private async Task DeleteTreatmentAsync()
        {
            if (SelectedTreatment == null) return;

            var result = _dialogService.ShowConfirmation($"¿Eliminar PERMANENTEMENTE el tratamiento '{SelectedTreatment.Name}'? Esta acción no se puede deshacer.",
                                             "Confirmar Eliminación Permanente");

            if (result == CoreDialogResult.No) return;

            var treatmentToDelete = await _treatmentRepository.GetByIdAsync(SelectedTreatment.Id);
            if (treatmentToDelete != null)
            {
                _treatmentRepository.Remove(treatmentToDelete);
                await _treatmentRepository.SaveChangesAsync();
                _dialogService.ShowMessage("Tratamiento eliminado.", "Eliminado");
            }

            await LoadTreatmentsAsync();
            TreatmentFormModel = new Treatment { IsActive = true };
            IsFormEnabled = false;
            SelectedTreatment = null;
        }

        // --- MÉTODO MANUAL DE NOTIFICACIÓN ---
        private void OnSelectedTreatmentChanged(Treatment? value)
        {
            IsFormEnabled = false;
            EditTreatmentCommand.NotifyCanExecuteChanged();
            DeleteTreatmentCommand.NotifyCanExecuteChanged();
        }
    }
}