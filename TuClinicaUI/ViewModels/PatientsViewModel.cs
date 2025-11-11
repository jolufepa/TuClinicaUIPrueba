// En: TuClinicaUI/ViewModels/PatientsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.UI.Views;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;
using TuClinica.Core.Extensions;
using System.Threading;

namespace TuClinica.UI.ViewModels
{
    public partial class PatientsViewModel : BaseViewModel
    {
        private readonly IPatientRepository _patientRepository;
        private readonly IValidationService _validationService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly PatientFileViewModel _patientFileViewModel;
        private readonly IActivityLogService _activityLogService;
        private readonly IDialogService _dialogService;
        private ICommand? _navigateToPatientFileCommand;

        private CancellationTokenSource? _searchCts;

        [ObservableProperty]
        private ObservableCollection<Patient> _patients = new ObservableCollection<Patient>();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPatientSelected))]
        [NotifyPropertyChangedFor(nameof(IsPatientSelectedAndActive))]
        [NotifyPropertyChangedFor(nameof(IsPatientSelectedAndInactive))]
        // --- AÑADIR NOTIFICACIÓN PARA EL NUEVO COMANDO ---
        [NotifyCanExecuteChangedFor(nameof(DeletePatientPermanentlyAsyncCommand))]
        private Patient? _selectedPatient;

        [ObservableProperty]
        private Patient _patientFormModel = new Patient();

        [ObservableProperty]
        private bool _isFormEnabled = false;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _showInactivePatients = false;

        public bool IsPatientSelected => SelectedPatient != null;
        public bool IsPatientSelectedAndActive => SelectedPatient != null && SelectedPatient.IsActive;
        public bool IsPatientSelectedAndInactive => SelectedPatient != null && !SelectedPatient.IsActive;


        // --- Comandos Manuales ---
        public IAsyncRelayCommand SearchPatientsCommand { get; }
        public IRelayCommand SetNewPatientFormCommand { get; }
        public IRelayCommand EditPatientCommand { get; }
        public IAsyncRelayCommand SavePatientCommand { get; }
        public IAsyncRelayCommand DeletePatientAsyncCommand { get; }
        public IRelayCommand CreatePrescriptionCommand { get; }
        public IAsyncRelayCommand ViewPatientDetailsCommand { get; }
        public IAsyncRelayCommand ReactivatePatientAsyncCommand { get; }
        // --- NUEVO COMANDO AÑADIDO ---
        public IAsyncRelayCommand DeletePatientPermanentlyAsyncCommand { get; }


        public PatientsViewModel(IPatientRepository patientRepository,
                                 IValidationService validationService,
                                 IServiceScopeFactory scopeFactory,
                                 PatientFileViewModel patientFileViewModel,
                                 IActivityLogService activityLogService,
                                 IDialogService dialogService)
        {
            _patientRepository = patientRepository;
            _validationService = validationService;
            _scopeFactory = scopeFactory;
            _patientFileViewModel = patientFileViewModel;
            _activityLogService = activityLogService;
            _dialogService = dialogService;

            // Inicialización de comandos
            SearchPatientsCommand = new AsyncRelayCommand(LoggedSearchAsync);
            SetNewPatientFormCommand = new RelayCommand(SetNewPatientForm);
            EditPatientCommand = new RelayCommand(EditPatient, () => IsPatientSelected);
            SavePatientCommand = new AsyncRelayCommand(SavePatientAsync);
            DeletePatientAsyncCommand = new AsyncRelayCommand(DeletePatientAsync, () => IsPatientSelectedAndActive);
            ReactivatePatientAsyncCommand = new AsyncRelayCommand(ReactivatePatientAsync, () => IsPatientSelectedAndInactive);
            CreatePrescriptionCommand = new RelayCommand(CreatePrescription, () => IsPatientSelected);
            ViewPatientDetailsCommand = new AsyncRelayCommand(ViewPatientDetailsAsync, () => IsPatientSelected);

            // --- NUEVO COMANDO AÑADIDO ---
            DeletePatientPermanentlyAsyncCommand = new AsyncRelayCommand(DeletePatientPermanentlyAsync, () => IsPatientSelectedAndInactive);

            _ = SearchPatientsAsync();
        }

        public void SetNavigationCommand(ICommand navigationCommand)
        {
            _navigateToPatientFileCommand = navigationCommand;
        }

        private async Task ViewPatientDetailsAsync()
        {
            if (SelectedPatient == null) return;
            if (_navigateToPatientFileCommand == null)
            {
                _dialogService.ShowMessage("Error de navegación. El comando no está configurado.", "Error");
                return;
            }

            try
            {
                _activityLogService.LogAccessAsync(
                    entityType: "Patient",
                    entityId: SelectedPatient.Id,
                    details: $"Vio la ficha de: {SelectedPatient.PatientDisplayInfo}");

                await _patientFileViewModel.LoadPatient(SelectedPatient);


                _navigateToPatientFileCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al navegar a la ficha del paciente:\n{ex.Message}", "Error");
            }
        }

        private async Task LoggedSearchAsync()
        {
            string logDetails = string.IsNullOrWhiteSpace(SearchText) ?
                "Vio la lista completa de pacientes" :
                $"Buscó pacientes: '{SearchText}'";
            _activityLogService.LogAccessAsync(logDetails);
            await SearchPatientsAsync();
        }

        private async Task SearchPatientsAsync()
        {
            Patients.Clear();
            IEnumerable<Patient>? patientsFromDb;
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                patientsFromDb = await _patientRepository.SearchByNameOrDniAsync(SearchText, ShowInactivePatients);
            }
            else
            {
                patientsFromDb = await _patientRepository.GetAllAsync(ShowInactivePatients);
            }
            if (patientsFromDb != null)
            {
                foreach (var patient in patientsFromDb.OrderBy(p => p.Surname).ThenBy(p => p.Name))
                {
                    Patients.Add(patient);
                }
            }
        }

        private void SetNewPatientForm()
        {
            PatientFormModel = new Patient();
            SelectedPatient = null;
            IsFormEnabled = true;
        }

        private void EditPatient()
        {
            if (SelectedPatient == null) return;
            PatientFormModel = new Patient
            {
                Id = SelectedPatient.Id,
                Name = SelectedPatient.Name,
                Surname = SelectedPatient.Surname,
                DniNie = SelectedPatient.DniNie,
                DateOfBirth = SelectedPatient.DateOfBirth,
                Phone = SelectedPatient.Phone,
                Address = SelectedPatient.Address,
                Email = SelectedPatient.Email,
                Notes = SelectedPatient.Notes,
                IsActive = SelectedPatient.IsActive
            };
            IsFormEnabled = true;
        }

        private async Task SavePatientAsync()
        {
            PatientFormModel.Name = PatientFormModel.Name.ToTitleCase();
            PatientFormModel.Surname = PatientFormModel.Surname.ToTitleCase();
            PatientFormModel.DniNie = PatientFormModel.DniNie?.ToUpper().Trim() ?? string.Empty;
            PatientFormModel.Email = PatientFormModel.Email?.ToLower().Trim() ?? string.Empty;

            if (!_validationService.IsValidDniNie(PatientFormModel.DniNie))
            {
                _dialogService.ShowMessage("El DNI o NIE introducido no tiene un formato válido.", "DNI/NIE Inválido");
                return;
            }

            try
            {
                if (PatientFormModel.Id == 0) // Nuevo
                {
                    var existingByDni = await _patientRepository.FindAsync(p => p.DniNie.ToUpper() == PatientFormModel.DniNie);
                    if (existingByDni != null && existingByDni.Any())
                    {
                        _dialogService.ShowMessage($"El DNI/NIE '{PatientFormModel.DniNie}' ya existe.", "DNI/NIE Duplicado");
                        return;
                    }
                    await _patientRepository.AddAsync(PatientFormModel);
                    _dialogService.ShowMessage("Paciente creado con éxito.", "Éxito");
                }
                else // Editar
                {
                    var existingPatient = await _patientRepository.GetByIdAsync(PatientFormModel.Id);
                    if (existingPatient != null)
                    {
                        if (!string.Equals(existingPatient.DniNie, PatientFormModel.DniNie, StringComparison.OrdinalIgnoreCase))
                        {
                            var duplicateCheck = await _patientRepository.FindAsync(p => p.DniNie.ToUpper() == PatientFormModel.DniNie && p.Id != PatientFormModel.Id);
                            if (duplicateCheck != null && duplicateCheck.Any())
                            {
                                _dialogService.ShowMessage($"El DNI/NIE '{PatientFormModel.DniNie}' ya está asignado a otro paciente.", "DNI/NIE Duplicado");
                                return;
                            }
                        }

                        existingPatient.Name = PatientFormModel.Name;
                        existingPatient.Surname = PatientFormModel.Surname;
                        existingPatient.DniNie = PatientFormModel.DniNie;
                        existingPatient.DateOfBirth = PatientFormModel.DateOfBirth;
                        existingPatient.Phone = PatientFormModel.Phone;
                        existingPatient.Address = PatientFormModel.Address;
                        existingPatient.Email = PatientFormModel.Email;
                        existingPatient.Notes = PatientFormModel.Notes;
                        existingPatient.IsActive = PatientFormModel.IsActive;

                        _patientRepository.Update(existingPatient);
                        _dialogService.ShowMessage("Paciente actualizado con éxito.", "Éxito");
                    }
                    else
                    {
                        _dialogService.ShowMessage("Error: No se encontró el paciente que intentaba editar.", "Error");
                        return;
                    }
                }

                await _patientRepository.SaveChangesAsync();
                await SearchPatientsAsync();
                PatientFormModel = new Patient();
                IsFormEnabled = false;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al guardar el paciente:\n{ex.Message}", "Error Base de Datos");
            }
        }

        private async Task DeletePatientAsync()
        {
            if (SelectedPatient == null) return;

            var result = _dialogService.ShowConfirmation($"¿Está seguro de archivar al paciente '{SelectedPatient.Name} {SelectedPatient.Surname}'?\n\nEl paciente se ocultará de la lista principal.", "Confirmar Archivación");
            if (result == CoreDialogResult.No) return;

            bool hasHistory = await _patientRepository.HasHistoryAsync(SelectedPatient.Id);

            if (hasHistory)
            {
                var p = await _patientRepository.GetByIdAsync(SelectedPatient.Id);
                if (p != null)
                {
                    p.IsActive = false;
                    _patientRepository.Update(p);
                    await _patientRepository.SaveChangesAsync();
                    _dialogService.ShowMessage("Paciente archivado (tenía historial).", "Archivado");
                }
            }
            else
            {
                var conf = _dialogService.ShowConfirmation("Este paciente no tiene historial. ¿Desea ELIMINARLO PERMANENTEMENTE?\n\n'No' = Solo archivar.", "Eliminación Permanente");

                var p = await _patientRepository.GetByIdAsync(SelectedPatient.Id);
                if (p == null) return;

                if (conf == CoreDialogResult.Yes) // BORRADO PERMANENTE
                {
                    _patientRepository.Remove(p);
                    await _patientRepository.SaveChangesAsync();
                    _dialogService.ShowMessage("Paciente eliminado permanentemente.", "Eliminado");
                }
                else // SOLO ARCHIVAR
                {
                    p.IsActive = false;
                    _patientRepository.Update(p);
                    await _patientRepository.SaveChangesAsync();
                    _dialogService.ShowMessage("Paciente archivado.", "Archivado");
                }
            }
            await SearchPatientsAsync();
            PatientFormModel = new Patient();
            IsFormEnabled = false;
            SelectedPatient = null;
        }

        private async Task ReactivatePatientAsync()
        {
            if (SelectedPatient == null) return;

            var result = _dialogService.ShowConfirmation($"¿Está seguro de reactivar al paciente '{SelectedPatient.Name} {SelectedPatient.Surname}'?\n\nVolverá a aparecer en la lista principal.", "Confirmar Reactivación");
            if (result == CoreDialogResult.No) return;

            try
            {
                var p = await _patientRepository.GetByIdAsync(SelectedPatient.Id);
                if (p != null)
                {
                    p.IsActive = true;
                    _patientRepository.Update(p);
                    await _patientRepository.SaveChangesAsync();
                    _dialogService.ShowMessage("Paciente reactivado con éxito.", "Éxito");

                    await LoggedSearchAsync();
                    SelectedPatient = null;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al reactivar el paciente:\n{ex.Message}", "Error Base de Datos");
            }
        }

        // --- NUEVO MÉTODO AÑADIDO ---
        private async Task DeletePatientPermanentlyAsync()
        {
            if (SelectedPatient == null) return;

            // Doble confirmación muy explícita
            var result = _dialogService.ShowConfirmation(
                $"¡ADVERTENCIA! Está a punto de ELIMINAR PERMANENTEMENTE a '{SelectedPatient.Name} {SelectedPatient.Surname}'.\n\n" +
                $"Esta acción NO SE PUEDE DESHACER y borrará toda la información del paciente.\n\n" +
                $"¿ESTÁ COMPLETAMENTE SEGURO?",
                "Confirmar Eliminación Permanente");

            if (result == CoreDialogResult.No) return;

            try
            {
                var p = await _patientRepository.GetByIdAsync(SelectedPatient.Id);
                if (p != null)
                {
                    _patientRepository.Remove(p);
                    await _patientRepository.SaveChangesAsync();
                    _dialogService.ShowMessage("Paciente eliminado permanentemente.", "Eliminado");

                    await LoggedSearchAsync();
                    SelectedPatient = null;
                }
            }
            catch (Exception ex)
            {
                // Este error es común si el paciente todavía tiene historial (Claves Foráneas)
                _dialogService.ShowMessage($"Error al eliminar permanentemente al paciente:\n{ex.Message}\n\n" +
                    $"Esto puede ocurrir si el paciente tiene historial clínico, presupuestos o pagos que deben ser eliminados primero.", "Error Base de Datos");
            }
        }


        private void CreatePrescription()
        {
            if (SelectedPatient == null)
            {
                _dialogService.ShowMessage("Seleccione un paciente primero.", "Paciente no seleccionado");
                return;
            }
            _dialogService.ShowMessage($"Funcionalidad pendiente: Abrir diálogo de receta para {SelectedPatient.Name} (ID: {SelectedPatient.Id}).", "Pendiente");
        }

        // --- MÉTODOS PARCIALES (Notificación de cambios) ---

        partial void OnSelectedPatientChanged(Patient? value)
        {
            IsFormEnabled = false;
            EditPatientCommand.NotifyCanExecuteChanged();
            CreatePrescriptionCommand.NotifyCanExecuteChanged();
            ViewPatientDetailsCommand.NotifyCanExecuteChanged();

            OnPropertyChanged(nameof(IsPatientSelectedAndActive));
            OnPropertyChanged(nameof(IsPatientSelectedAndInactive));
            DeletePatientAsyncCommand.NotifyCanExecuteChanged();
            ReactivatePatientAsyncCommand.NotifyCanExecuteChanged();
            // --- AÑADIDO ---
            DeletePatientPermanentlyAsyncCommand.NotifyCanExecuteChanged();
        }

        partial void OnSearchTextChanged(string value)
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            _ = DebouncedSearchAsync(_searchCts.Token);
        }

        partial void OnShowInactivePatientsChanged(bool value)
        {
            _ = LoggedSearchAsync();
        }

        private async Task DebouncedSearchAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(300, token);
                await LoggedSearchAsync();
            }
            catch (TaskCanceledException)
            {
                // No hacer nada
            }
        }
    }
}