// En: TuClinicaUI/ViewModels/TreatmentsViewModel.cs
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

// --- AÑADIR 'partial' ---
namespace TuClinica.UI.ViewModels
{
    public partial class TreatmentsViewModel : BaseViewModel
    {
        private readonly ITreatmentRepository _treatmentRepository;
        private readonly IDialogService _dialogService;

        // --- Propiedades con Generadores de Código ---

        [ObservableProperty]
        private ObservableCollection<Treatment> _treatments = new ObservableCollection<Treatment>();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsTreatmentSelected))]
        // Notifica automáticamente a los comandos cuando el seleccionado cambia
        [NotifyCanExecuteChangedFor(nameof(EditTreatmentCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteTreatmentCommand))]
        private Treatment? _selectedTreatment;

        [ObservableProperty]
        private Treatment _treatmentFormModel = new Treatment { IsActive = true };

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveTreatmentCommand))] // Habilitar/Deshabilitar Guardar
        private bool _isFormEnabled = false;

        public bool IsTreatmentSelected => SelectedTreatment != null;


        // --- Constructor (limpiado de inicializaciones de comandos) ---
        public TreatmentsViewModel(ITreatmentRepository treatmentRepository, IDialogService dialogService)
        {
            _treatmentRepository = treatmentRepository;
            _dialogService = dialogService;

            _ = LoadTreatmentsAsync();
        }

        // --- MÉTODOS DE COMANDOS (Convertidos a Generadores) ---

        [RelayCommand]
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

        [RelayCommand]
        private void SetNewTreatmentForm()
        {
            TreatmentFormModel = new Treatment { IsActive = true };
            IsFormEnabled = true;
            SelectedTreatment = null;
        }

        [RelayCommand(CanExecute = nameof(IsTreatmentSelected))]
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

        [RelayCommand(CanExecute = nameof(IsFormEnabled))] // <-- CanExecute añadido
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
                    // --- CORRECCIÓN: Llamar a Update() síncrono ---
                    _treatmentRepository.Update(existingTreatment);
                }
                else { return; }
            }

            await _treatmentRepository.SaveChangesAsync();
            await LoadTreatmentsAsync();
            TreatmentFormModel = new Treatment { IsActive = true };
            IsFormEnabled = false;
        }

        [RelayCommand(CanExecute = nameof(IsTreatmentSelected))]
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

        // --- MÉTODO OnSelectedTreatmentChanged ELIMINADO ---
        // (Ya no es necesario, [NotifyCanExecuteChangedFor] lo hace automáticamente)
    }
}