// En: TuClinicaUI/ViewModels/PatientsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services; // <-- Asegúrate de tener este
using TuClinica.Core.Models;
using System.Windows;
using System.Windows.Input;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System;
using Microsoft.Extensions.DependencyInjection;
using TuClinica.UI.Views;
using System.Linq.Expressions;

namespace TuClinica.UI.ViewModels
{
    public partial class PatientsViewModel : BaseViewModel
    {
        private readonly IPatientRepository _patientRepository;
        private readonly IValidationService _validationService;
        private readonly IServiceProvider _serviceProvider;
        private readonly PatientFileViewModel _patientFileViewModel;
        private readonly IActivityLogService _activityLogService; // <-- Inyección del Log
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
                                 IActivityLogService activityLogService) // <-- Inyección
        {
            _patientRepository = patientRepository;
            _validationService = validationService;
            _serviceProvider = serviceProvider;
            _patientFileViewModel = patientFileViewModel;
            _activityLogService = activityLogService; // <-- Asignación

            // Inicialización de comandos
            // === CORRECCIÓN 1: Apuntar al nuevo método "LoggedSearchAsync" ===
            SearchPatientsCommand = new AsyncRelayCommand(LoggedSearchAsync);

            SetNewPatientFormCommand = new RelayCommand(SetNewPatientForm);
            EditPatientCommand = new RelayCommand(EditPatient, () => IsPatientSelected);
            SavePatientCommand = new AsyncRelayCommand(SavePatientAsync);
            DeletePatientAsyncCommand = new AsyncRelayCommand(DeletePatientAsync, () => IsPatientSelected);
            CreatePrescriptionCommand = new RelayCommand(CreatePrescription, () => IsPatientSelected);
            ViewPatientDetailsCommand = new RelayCommand(ViewPatientDetails, () => IsPatientSelected);

            // === CORRECCIÓN 2: Carga inicial SIN log ===
            _ = SearchPatientsAsync(); // Carga inicial (Llama al método base)
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
                MessageBox.Show("Error de navegación. El comando no está configurado.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Error al navegar a la ficha del paciente:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // --- MÉTODOS DE COMANDOS ---

        // === CORRECCIÓN 3: Nuevo método "Wrapper" que SÍ registra el log ===
        private async Task LoggedSearchAsync()
        {
            // 1. Registrar la acción de búsqueda
            string logDetails = string.IsNullOrWhiteSpace(SearchText) ?
                "Vio la lista completa de pacientes" :
                $"Buscó pacientes: '{SearchText}'";
            _activityLogService.LogAccessAsync(logDetails);

            // 2. Ejecutar la búsqueda real
            await SearchPatientsAsync();
        }

        // --- Método de búsqueda base (SIN log y SIN parámetros) ---
        private async Task SearchPatientsAsync()
        {
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
                foreach (var patient in patientsFromDb.OrderBy(p => p.Surname).ThenBy(p => p.Name)) // Ordenado
                {
                    Patients.Add(patient);
                }
            }
        }

        private void SetNewPatientForm()
        {
            PatientFormModel = new Patient();
            IsFormEnabled = true;
            SelectedPatient = null;
        }

        private void EditPatient()
        {
            if (SelectedPatient == null) return;
            // Clonar paciente seleccionado al formulario
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
            // ... (Tu código existente para guardar) ...
            // Normalización
            PatientFormModel.Name = ToTitleCase(PatientFormModel.Name);
            PatientFormModel.Surname = ToTitleCase(PatientFormModel.Surname);
            PatientFormModel.DniNie = PatientFormModel.DniNie?.ToUpper().Trim() ?? string.Empty;
            PatientFormModel.Email = PatientFormModel.Email?.ToLower().Trim() ?? string.Empty;

            // Validación Formato
            if (!_validationService.IsValidDniNie(PatientFormModel.DniNie))
            {
                MessageBox.Show("El DNI o NIE introducido no tiene un formato válido.", "DNI/NIE InválIDO", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (PatientFormModel.Id == 0) // Nuevo
                {
                    var existingByDni = await _patientRepository.FindAsync(p => p.DniNie.ToUpper() == PatientFormModel.DniNie);
                    if (existingByDni != null && existingByDni.Any())
                    {
                        MessageBox.Show($"El DNI/NIE '{PatientFormModel.DniNie}' ya existe.", "DNI/NIE Duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    await _patientRepository.AddAsync(PatientFormModel);
                    MessageBox.Show("Paciente creado con éxito.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
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
                                MessageBox.Show($"El DNI/NIE '{PatientFormModel.DniNie}' ya está asignado a otro paciente.", "DNI/NIE Duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        MessageBox.Show("Paciente actualizado con éxito.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Error: No se encontró el paciente que intentaba editar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Error al guardar el paciente:\n{ex.Message}", "Error Base de Datos", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // (No se necesita log aquí, el DbContext lo hace)
        private async Task DeletePatientAsync()
        {
            if (SelectedPatient == null) return;
            var result = MessageBox.Show($"¿Eliminar/archivar '{SelectedPatient.Name} {SelectedPatient.Surname}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.No) return;

            bool hasHistory = await _patientRepository.HasHistoryAsync(SelectedPatient.Id);
            if (hasHistory)
            {
                var p = await _patientRepository.GetByIdAsync(SelectedPatient.Id);
                if (p != null)
                {
                    p.IsActive = false; // <-- Esto es un "Update" que el DbContext registrará
                    await _patientRepository.SaveChangesAsync();
                    MessageBox.Show("Paciente archivado (tenía historial).", "Archivado", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                var conf = MessageBox.Show("ELIMINACIÓN PERMANENTE. ¿Seguro?", "¡ADVERTENCIA!", MessageBoxButton.YesNo, MessageBoxImage.Stop);
                if (conf == MessageBoxResult.No) return;
                var p = await _patientRepository.GetByIdAsync(SelectedPatient.Id);
                if (p != null)
                {
                    _patientRepository.Remove(p); // <-- Esto es un "Delete" que el DbContext registrará
                    await _patientRepository.SaveChangesAsync();
                    MessageBox.Show("Paciente eliminado permanentemente.", "Eliminado", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show("Seleccione un paciente primero.", "Paciente no seleccionado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            MessageBox.Show($"Funcionalidad pendiente: Abrir diálogo de receta para {SelectedPatient.Name} (ID: {SelectedPatient.Id}).", "Pendiente");
        }

        // --- MÉTODOS PARCIALES (Notificación de cambios) ---

        partial void OnSelectedPatientChanged(Patient? value)
        {
            IsFormEnabled = false;
            EditPatientCommand.NotifyCanExecuteChanged();
            DeletePatientAsyncCommand.NotifyCanExecuteChanged();
            CreatePrescriptionCommand.NotifyCanExecuteChanged();
            ViewPatientDetailsCommand.NotifyCanExecuteChanged();
        }

        partial void OnSearchTextChanged(string value)
        {
            // === CORRECCIÓN 4: Apuntar al método con log ===
            _ = LoggedSearchAsync();
        }

        // Método de Ayuda
        private string ToTitleCase(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower());
        }
    }
}