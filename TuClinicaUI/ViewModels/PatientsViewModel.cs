// En: TuClinicaUI/ViewModels/PatientsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
//using System.Windows; // Ya no necesitamos 'System.Windows' para MessageBox
using System.Windows.Input;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System;
using Microsoft.Extensions.DependencyInjection;
using TuClinica.UI.Views;
using System.Linq.Expressions;
// Usamos un alias para evitar conflictos entre nuestro enum y el de WPF
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;

namespace TuClinica.UI.ViewModels
{
    public partial class PatientsViewModel : BaseViewModel
    {
        private readonly IPatientRepository _patientRepository;
        private readonly IValidationService _validationService;
        private readonly IServiceProvider _serviceProvider;
        private readonly PatientFileViewModel _patientFileViewModel;
        private readonly IActivityLogService _activityLogService; // <-- Inyección del Log
        private readonly IDialogService _dialogService; // <-- CAMBIO: Servicio de Diálogo
        private ICommand? _navigateToPatientFileCommand;

        // --- Propiedades Observables ---
        [ObservableProperty]
        private ObservableCollection<Patient> _patients = new ObservableCollection<Patient>();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPatientSelected))]
        private Patient? _selectedPatient;

        [ObservableProperty]
        private Patient _patientFormModel = new Patient();

        [ObservableProperty]
        private bool _isFormEnabled = false;

        [ObservableProperty]
        private string _searchText = string.Empty;

        public bool IsPatientSelected => SelectedPatient != null;

        // --- Comandos Manuales ---
        public IAsyncRelayCommand SearchPatientsCommand { get; }
        public IRelayCommand SetNewPatientFormCommand { get; }
        public IRelayCommand EditPatientCommand { get; }
        public IAsyncRelayCommand SavePatientCommand { get; }
        public IAsyncRelayCommand DeletePatientAsyncCommand { get; }
        public IRelayCommand CreatePrescriptionCommand { get; }
        public IRelayCommand ViewPatientDetailsCommand { get; }


        // *** CONSTRUCTOR MODIFICADO ***
        public PatientsViewModel(IPatientRepository patientRepository,
                                 IValidationService validationService,
                                 IServiceProvider serviceProvider,
                                 PatientFileViewModel patientFileViewModel,
                                 IActivityLogService activityLogService,
                                 IDialogService dialogService) // <-- Inyección
        {
            _patientRepository = patientRepository;
            _validationService = validationService;
            _serviceProvider = serviceProvider;
            _patientFileViewModel = patientFileViewModel;
            _activityLogService = activityLogService;
            _dialogService = dialogService; // <-- CAMBIO: Asignación

            // Inicialización de comandos
            SearchPatientsCommand = new AsyncRelayCommand(LoggedSearchAsync);
            SetNewPatientFormCommand = new RelayCommand(SetNewPatientForm);
            EditPatientCommand = new RelayCommand(EditPatient, () => IsPatientSelected);
            SavePatientCommand = new AsyncRelayCommand(SavePatientAsync);
            DeletePatientAsyncCommand = new AsyncRelayCommand(DeletePatientAsync, () => IsPatientSelected);
            CreatePrescriptionCommand = new RelayCommand(CreatePrescription, () => IsPatientSelected);
            ViewPatientDetailsCommand = new RelayCommand(ViewPatientDetails, () => IsPatientSelected);

            _ = SearchPatientsAsync();
        }

        public void SetNavigationCommand(ICommand navigationCommand)
        {
            _navigateToPatientFileCommand = navigationCommand;
        }

        private void ViewPatientDetails()
        {
            if (SelectedPatient == null) return;
            if (_navigateToPatientFileCommand == null)
            {
                // --- CAMBIO ---
                _dialogService.ShowMessage("Error de navegación. El comando no está configurado.", "Error");
                return;
            }

            try
            {
                // --- Log de Acceso (Lectura) ---
                _activityLogService.LogAccessAsync(
                    entityType: "Patient",
                    entityId: SelectedPatient.Id,
                    details: $"Vio la ficha de: {SelectedPatient.PatientDisplayInfo}");
                // --- Fin Log ---

                _patientFileViewModel.LoadPatient(SelectedPatient);
                _navigateToPatientFileCommand.Execute(null);
            }
            catch (Exception ex)
            {
                // --- CAMBIO ---
                _dialogService.ShowMessage($"Error al navegar a la ficha del paciente:\n{ex.Message}", "Error");
            }
        }


        // --- MÉTODOS DE COMANDOS ---

        private async Task LoggedSearchAsync()
        {
            // ... (Sin cambios)
            string logDetails = string.IsNullOrWhiteSpace(SearchText) ?
                "Vio la lista completa de pacientes" :
                $"Buscó pacientes: '{SearchText}'";
            _activityLogService.LogAccessAsync(logDetails);
            await SearchPatientsAsync();
        }

        private async Task SearchPatientsAsync()
        {
            // ... (Sin cambios)
            Patients.Clear();
            IEnumerable<Patient>? patientsFromDb;
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                patientsFromDb = await _patientRepository.SearchByNameOrDniAsync(SearchText);
            }
            else
            {
                patientsFromDb = await _patientRepository.GetAllAsync();
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
            // ... (Sin cambios)
            if (SelectedPatient == null) return;
            PatientFormModel = new Patient
            {
                Id = SelectedPatient.Id,
                Name = SelectedPatient.Name,
                Surname = SelectedPatient.Surname,
                DniNie = SelectedPatient.DniNie,
                Phone = SelectedPatient.Phone,
                Address = SelectedPatient.Address,
                Email = SelectedPatient.Email,
                Notes = SelectedPatient.Notes,
                IsActive = SelectedPatient.IsActive
            };
            IsFormEnabled = true;
        }

        // (No se necesita log aquí, el DbContext lo hace)
        private async Task SavePatientAsync()
        {
            // Normalización
            PatientFormModel.Name = ToTitleCase(PatientFormModel.Name);
            PatientFormModel.Surname = ToTitleCase(PatientFormModel.Surname);
            PatientFormModel.DniNie = PatientFormModel.DniNie?.ToUpper().Trim() ?? string.Empty;
            PatientFormModel.Email = PatientFormModel.Email?.ToLower().Trim() ?? string.Empty;

            // Validación Formato
            if (!_validationService.IsValidDniNie(PatientFormModel.DniNie))
            {
                // (Esta ya la tenías cambiada, ¡bien!)
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
                        // --- CAMBIO ---
                        _dialogService.ShowMessage($"El DNI/NIE '{PatientFormModel.DniNie}' ya existe.", "DNI/NIE Duplicado");
                        return;
                    }
                    await _patientRepository.AddAsync(PatientFormModel);
                    // --- CAMBIO ---
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
                                // --- CAMBIO ---
                                _dialogService.ShowMessage($"El DNI/NIE '{PatientFormModel.DniNie}' ya está asignado a otro paciente.", "DNI/NIE Duplicado");
                                return;
                            }
                        }
                        // Actualizar
                        existingPatient.Name = PatientFormModel.Name;
                        existingPatient.Surname = PatientFormModel.Surname;
                        existingPatient.DniNie = PatientFormModel.DniNie;
                        existingPatient.Phone = PatientFormModel.Phone;
                        existingPatient.Address = PatientFormModel.Address;
                        existingPatient.Email = PatientFormModel.Email;
                        existingPatient.Notes = PatientFormModel.Notes;
                        _patientRepository.Update(existingPatient);
                        // --- CAMBIO ---
                        _dialogService.ShowMessage("Paciente actualizado con éxito.", "Éxito");
                    }
                    else
                    {
                        // --- CAMBIO ---
                        _dialogService.ShowMessage("Error: No se encontró el paciente que intentaba editar.", "Error");
                        return;
                    }
                }

                await _patientRepository.SaveChangesAsync();
                await SearchPatientsAsync(); // Recarga la lista (sin log)
                PatientFormModel = new Patient();
                IsFormEnabled = false;
            }
            catch (Exception ex)
            {
                // --- CAMBIO ---
                _dialogService.ShowMessage($"Error al guardar el paciente:\n{ex.Message}", "Error Base de Datos");
            }
        }

        // (No se necesita log aquí, el DbContext lo hace)
        private async Task DeletePatientAsync()
        {
            if (SelectedPatient == null) return;

            // (Esta ya la tenías cambiada, ¡bien!)
            var result = _dialogService.ShowConfirmation($"¿Eliminar/archivar '{SelectedPatient.Name} {SelectedPatient.Surname}'?", "Confirmar");
            if (result == CoreDialogResult.No) return;

            bool hasHistory = await _patientRepository.HasHistoryAsync(SelectedPatient.Id);
            if (hasHistory)
            {
                var p = await _patientRepository.GetByIdAsync(SelectedPatient.Id);
                if (p != null)
                {
                    p.IsActive = false; // <-- Esto es un "Update" que el DbContext registrará
                    await _patientRepository.SaveChangesAsync();
                    // --- CAMBIO ---
                    _dialogService.ShowMessage("Paciente archivado (tenía historial).", "Archivado");
                }
            }
            else
            {
                // --- CAMBIO ---
                var conf = _dialogService.ShowConfirmation("ELIMINACIÓN PERMANENTE. ¿Seguro?", "¡ADVERTENCIA!");
                if (conf == CoreDialogResult.No) return; // <-- CAMBIO (usar CoreDialogResult)

                var p = await _patientRepository.GetByIdAsync(SelectedPatient.Id);
                if (p != null)
                {
                    _patientRepository.Remove(p); // <-- Esto es un "Delete" que el DbContext registrará
                    await _patientRepository.SaveChangesAsync();
                    // --- CAMBIO ---
                    _dialogService.ShowMessage("Paciente eliminado permanentemente.", "Eliminado");
                }
            }
            await SearchPatientsAsync(); // Recarga la lista (sin log)
            PatientFormModel = new Patient();
            IsFormEnabled = false;
            SelectedPatient = null;
        }

        private void CreatePrescription()
        {
            if (SelectedPatient == null)
            {
                // --- CAMBIO ---
                _dialogService.ShowMessage("Seleccione un paciente primero.", "Paciente no seleccionado");
                return;
            }
            // --- CAMBIO ---
            _dialogService.ShowMessage($"Funcionalidad pendiente: Abrir diálogo de receta para {SelectedPatient.Name} (ID: {SelectedPatient.Id}).", "Pendiente");
        }

        // --- MÉTODOS PARCIALES (Notificación de cambios) ---

        partial void OnSelectedPatientChanged(Patient? value)
        {
            // ... (Sin cambios)
            IsFormEnabled = false;
            EditPatientCommand.NotifyCanExecuteChanged();
            DeletePatientAsyncCommand.NotifyCanExecuteChanged();
            CreatePrescriptionCommand.NotifyCanExecuteChanged();
            ViewPatientDetailsCommand.NotifyCanExecuteChanged();
        }

        partial void OnSearchTextChanged(string value)
        {
            // ... (Sin cambios)
            _ = LoggedSearchAsync();
        }

        // Método de Ayuda
        private string ToTitleCase(string text)
        {
            // ... (Sin cambios)
            if (string.IsNullOrWhiteSpace(text)) return text;
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower());
        }
    }
}