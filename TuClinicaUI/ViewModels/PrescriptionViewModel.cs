using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input; // Para ICommand
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.DataAccess;
using TuClinica.UI.Views;
using TuClinica.Core.Interfaces;
// Añadido para el diálogo de confirmación
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;


namespace TuClinica.UI.ViewModels
{
    // Asegúrate de que la clase sigue siendo 'public partial class'
    public partial class PrescriptionViewModel : BaseViewModel
    {
        // Servicios (sin cambios)
        private readonly IServiceProvider _serviceProvider;
        private readonly IPdfService _pdfService;
        private readonly IMedicationRepository _medicationRepository;
        private readonly IDosageRepository _dosageRepository;
        private readonly IRepository<Prescription> _prescriptionRepository;
        private readonly IDialogService _dialogService;

        // --- Pestaña "Crear Receta" (sin cambios) ---
        private Patient? _selectedPatient;
        public Patient? SelectedPatient
        {
            get => _selectedPatient;
            set
            {
                if (SetProperty(ref _selectedPatient, value))
                {
                    OnSelectedPatientChanged(value);
                    GeneratePrescriptionPdfCommand.NotifyCanExecuteChanged();
                    GenerateBasicPrescriptionPdfCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _selectedPatientFullNameDisplay = "Ningún paciente seleccionado";
        public string SelectedPatientFullNameDisplay
        {
            get => _selectedPatientFullNameDisplay;
            set => SetProperty(ref _selectedPatientFullNameDisplay, value);
        }

        private string _medicationSearchText = string.Empty;
        public string MedicationSearchText
        {
            get => _medicationSearchText;
            set
            {
                if (SetProperty(ref _medicationSearchText, value))
                {
                    GeneratePrescriptionPdfCommand.NotifyCanExecuteChanged();
                    GenerateBasicPrescriptionPdfCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _dosageSearchText = string.Empty;
        public string DosageSearchText
        {
            get => _dosageSearchText;
            set
            {
                if (SetProperty(ref _dosageSearchText, value))
                {
                    GeneratePrescriptionPdfCommand.NotifyCanExecuteChanged();
                    GenerateBasicPrescriptionPdfCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string MedicationQuantity { get; set; } = "1";
        public string TreatmentDuration { get; set; } = "10 días";
        public string Instructions { get; set; } = string.Empty;


        // --- Pestaña "Gestionar Medicamentos" (MODIFICADA) ---
        public ObservableCollection<Medication> Medications { get; set; } = new();

        // Propiedad MODIFICADA para notificar a los comandos
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(EditMedicationCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteMedicationAsyncCommand))]
        private Medication? _selectedMedication;

        [ObservableProperty]
        private string _newMedicationName = string.Empty;

        [ObservableProperty]
        private string _newMedicationPresentation = string.Empty;

        // NUEVA propiedad para controlar el estado del formulario (Nuevo vs Editar)
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormTitle))]
        private bool _isEditingMedication = false;

        // NUEVA propiedad para el título del formulario
        public string FormTitle => IsEditingMedication ? "Editar Medicamento" : "Añadir Nuevo Medicamento";


        // --- Pestaña "Gestionar Pautas" (sin cambios) ---
        public ObservableCollection<Dosage> Dosages { get; set; } = new();
        public Dosage? SelectedDosage { get; set; }
        public string NewDosagePauta { get; set; } = string.Empty;

        // --- COMANDOS MANUALES (IRelayCommand) ---
        public IRelayCommand SelectPatientCommand { get; }
        public IAsyncRelayCommand GeneratePrescriptionPdfCommand { get; }
        public IAsyncRelayCommand GenerateBasicPrescriptionPdfCommand { get; }
        public IAsyncRelayCommand LoadMedicationsCommand { get; }
        // MODIFICADO: Cambiado el nombre para reflejar que es Async
        public IAsyncRelayCommand SaveMedicationAsyncCommand { get; }
        public IAsyncRelayCommand LoadDosagesCommand { get; }
        public IAsyncRelayCommand SaveDosageCommand { get; }

        // --- NUEVOS COMANDOS ---
        public IRelayCommand EditMedicationCommand { get; }
        public IAsyncRelayCommand DeleteMedicationAsyncCommand { get; }
        public IRelayCommand ClearMedicationFormCommand { get; }


        public PrescriptionViewModel(
            IServiceProvider serviceProvider,
            IPdfService pdfService,
            IMedicationRepository medicationRepository,
            IDosageRepository dosageRepository,
            IRepository<Prescription> prescriptionRepository,
            IDialogService dialogService)
        {
            _serviceProvider = serviceProvider;
            _pdfService = pdfService;
            _medicationRepository = medicationRepository;
            _dosageRepository = dosageRepository;
            _prescriptionRepository = prescriptionRepository;
            _dialogService = dialogService;

            // Inicialización de comandos (Crear Receta)
            SelectPatientCommand = new RelayCommand(SelectPatient);
            GeneratePrescriptionPdfCommand = new AsyncRelayCommand(GeneratePrescriptionPdfAsync, CanGeneratePrescription);
            GenerateBasicPrescriptionPdfCommand = new AsyncRelayCommand(GenerateBasicPrescriptionPdfAsync, CanGeneratePrescription);

            // Inicialización de comandos (Gestionar Medicamentos) - MODIFICADO
            LoadMedicationsCommand = new AsyncRelayCommand(LoadMedicationsAsync);
            SaveMedicationAsyncCommand = new AsyncRelayCommand(SaveMedicationAsync); // Nombre cambiado aquí
            ClearMedicationFormCommand = new RelayCommand(ClearMedicationForm); // NUEVO
            EditMedicationCommand = new RelayCommand(EditMedication, CanEditOrDelete); // NUEVO
            DeleteMedicationAsyncCommand = new AsyncRelayCommand(DeleteMedicationAsync, CanEditOrDelete); // NUEVO

            // Inicialización de comandos (Gestionar Pautas)
            LoadDosagesCommand = new AsyncRelayCommand(LoadDosagesAsync);
            SaveDosageCommand = new AsyncRelayCommand(SaveDosageAsync);


            _ = LoadInitialDataAsync();
        }

        private async Task LoadInitialDataAsync()
        {
            await LoadMedicationsAsync();
            await LoadDosagesAsync();
        }

        // --- Lógica Pestaña "Crear Receta" (sin cambios) ---

        private void SelectPatient()
        {
            try
            {
                var dialog = _serviceProvider.GetRequiredService<PatientSelectionDialog>();

                Window? ownerWindow = Application.Current.MainWindow;
                if (ownerWindow != null && ownerWindow != dialog)
                {
                    dialog.Owner = ownerWindow;
                }

                var result = dialog.ShowDialog();

                var dialogViewModel = dialog.ViewModel;
                if (dialogViewModel == null) return;

                if (result == true && dialogViewModel.SelectedPatientFromList != null)
                {
                    SelectedPatient = dialogViewModel.SelectedPatientFromList;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al abrir la selección de paciente:\n{ex.Message}", "Error");
            }
        }

        private Medication? _selectedMedicationForPrescription;
        public Medication? SelectedMedicationForPrescription
        {
            get => _selectedMedicationForPrescription;
            set
            {
                if (SetProperty(ref _selectedMedicationForPrescription, value))
                {
                    if (value != null)
                    {
                        MedicationSearchText = value.FullDisplay;
                    }
                }
            }
        }

        private async Task<Prescription?> CreateAndSavePrescriptionAsync()
        {
            if (!CanGeneratePrescription())
            {
                _dialogService.ShowMessage("Debe seleccionar un paciente e introducir un medicamento y una pauta.", "Datos incompletos");
                return null;
            }
            var settings = _serviceProvider.GetRequiredService<AppSettings>();
            var authService = _serviceProvider.GetRequiredService<IAuthService>();
            var currentUser = authService.CurrentUser;
            var prescription = new Prescription
            {
                PatientId = SelectedPatient!.Id,
                Patient = SelectedPatient,
                IssueDate = DateTime.Now,
                Instructions = this.Instructions,
                PrescriptorName = currentUser?.Username ?? settings.ClinicName,
                PrescriptorCollegeNum = currentUser?.CollegeNumber ?? string.Empty,
                PrescriptorSpecialty = currentUser?.Specialty ?? "General",
            };
            var item = new PrescriptionItem
            {
                MedicationName = this.MedicationSearchText,
                DosagePauta = this.DosageSearchText,
                Duration = this.TreatmentDuration,
                Quantity = this.MedicationQuantity,
                Prescription = prescription
            };
            prescription.Items.Add(item);
            try
            {
                prescription.Patient = null;
                await _prescriptionRepository.AddAsync(prescription);
                await _prescriptionRepository.SaveChangesAsync();
                return prescription;
            }
            catch (Exception ex)
            {
                string innerExMessage = ex.InnerException?.Message ?? ex.Message;
                _dialogService.ShowMessage($"Error al guardar la receta en la BD: {innerExMessage}", "Error BD");
                return null;
            }
        }

        private async Task GeneratePrescriptionPdfAsync()
        {
            var prescription = await CreateAndSavePrescriptionAsync();
            if (prescription == null) return;

            string pdfPath = string.Empty;
            try
            {
                pdfPath = await _pdfService.GeneratePrescriptionPdfAsync(prescription);
                _dialogService.ShowMessage($"PDF de Receta Oficial generado para: {SelectedPatient!.Name}\nGuardado en: {pdfPath}", "Receta Generada");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(pdfPath) { UseShellExecute = true });
            }
            catch (Exception pdfEx)
            {
                _dialogService.ShowMessage($"Error al generar el PDF de la receta:\n{pdfEx.Message}", "Error PDF");
            }
            ClearForm();
        }

        private async Task GenerateBasicPrescriptionPdfAsync()
        {
            var prescription = await CreateAndSavePrescriptionAsync();
            if (prescription == null) return;
            string pdfPath = string.Empty;
            try
            {
                pdfPath = await _pdfService.GenerateBasicPrescriptionPdfAsync(prescription);
                _dialogService.ShowMessage($"PDF de Receta Básica generado para: {SelectedPatient!.Name}\nGuardado en: {pdfPath}", "Receta Generada");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(pdfPath) { UseShellExecute = true });
            }
            catch (Exception pdfEx)
            {
                _dialogService.ShowMessage($"Error al generar el PDF de la receta básica:\n{pdfEx.Message}", "Error PDF");
            }
            ClearForm();
        }

        private void ClearForm()
        {
            SelectedPatient = null;
            MedicationSearchText = string.Empty;
            DosageSearchText = string.Empty;
            Instructions = string.Empty;
            MedicationQuantity = "1";
            TreatmentDuration = "10 días";
            SelectedMedicationForPrescription = null;
        }

        private bool CanGeneratePrescription()
        {
            return SelectedPatient != null &&
                   !string.IsNullOrWhiteSpace(MedicationSearchText) &&
                   !string.IsNullOrWhiteSpace(DosageSearchText);
        }

        private void OnSelectedPatientChanged(Patient? value)
        {
            SelectedPatientFullNameDisplay = value?.PatientDisplayInfo ?? "Ningún paciente seleccionado";
        }

        // --- Lógica Pestaña "Gestionar Medicamentos" (MODIFICADA) ---

        // Método auxiliar para capitalización
        private string ToTitleCase(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            // Convierte todo a minúsculas y luego a "Title Case" (primera letra mayúscula)
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower().Trim());
        }

        private async Task LoadMedicationsAsync()
        {
            Medications.Clear();
            var meds = await _medicationRepository.GetAllActiveAsync();
            foreach (var m in meds) Medications.Add(m);
        }

        // NUEVO: Método CanExecute para los botones Editar/Eliminar
        private bool CanEditOrDelete()
        {
            return SelectedMedication != null;
        }

        // NUEVO: Comando para limpiar el formulario y salir del modo edición
        private void ClearMedicationForm()
        {
            NewMedicationName = string.Empty;
            NewMedicationPresentation = string.Empty;
            SelectedMedication = null; // Deselecciona la lista
            IsEditingMedication = false;
        }

        // NUEVO: Comando para poblar el formulario para editar
        private void EditMedication()
        {
            if (SelectedMedication == null) return;

            NewMedicationName = SelectedMedication.Name;
            NewMedicationPresentation = SelectedMedication.Presentation ?? string.Empty;
            IsEditingMedication = true;
        }

        // NUEVO: Comando para eliminar un medicamento
        private async Task DeleteMedicationAsync()
        {
            if (SelectedMedication == null) return;

            var result = _dialogService.ShowConfirmation(
                $"¿Está seguro de que desea eliminar permanentemente '{SelectedMedication.FullDisplay}'?\n\nEsta acción no se puede deshacer.",
                "Confirmar Eliminación");

            if (result == CoreDialogResult.No) return;

            try
            {
                _medicationRepository.Remove(SelectedMedication);
                await _medicationRepository.SaveChangesAsync();
                await LoadMedicationsAsync(); // Recargar la lista
                ClearMedicationForm(); // Limpiar el formulario
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al eliminar el medicamento: {ex.Message}", "Error BD");
            }
        }

        // MODIFICADO: Comando de guardar para manejar Nuevo y Editar
        private async Task SaveMedicationAsync()
        {
            // Aplicamos la capitalización
            string nameToSave = ToTitleCase(NewMedicationName);
            string presentationToSave = ToTitleCase(NewMedicationPresentation); // También capitalizamos la presentación

            if (string.IsNullOrWhiteSpace(nameToSave))
            {
                _dialogService.ShowMessage("El nombre del medicamento no puede estar vacío.", "Dato Requerido");
                return;
            }

            try
            {
                if (IsEditingMedication)
                {
                    // --- Lógica de ACTUALIZACIÓN ---
                    if (SelectedMedication == null)
                    {
                        _dialogService.ShowMessage("No hay ningún medicamento seleccionado para editar.", "Error");
                        return;
                    }

                    // Obtenemos la entidad rastreada por EF Core
                    var medToUpdate = await _medicationRepository.GetByIdAsync(SelectedMedication.Id);
                    if (medToUpdate == null)
                    {
                        _dialogService.ShowMessage("El medicamento que intenta editar ya no existe.", "Error");
                        return;
                    }

                    medToUpdate.Name = nameToSave;
                    medToUpdate.Presentation = presentationToSave;
                    _medicationRepository.Update(medToUpdate);
                }
                else
                {
                    // --- Lógica de CREACIÓN (la que ya tenías) ---
                    var newMed = new Medication
                    {
                        Name = nameToSave,
                        Presentation = presentationToSave
                    };
                    await _medicationRepository.AddAsync(newMed);
                }

                // Guardamos los cambios (ya sea Add o Update)
                await _medicationRepository.SaveChangesAsync();

                // Limpiamos y recargamos
                ClearMedicationForm();
                await LoadMedicationsAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al guardar el medicamento: {ex.Message}", "Error BD");
            }
        }

        // --- Lógica Pestaña "Gestionar Pautas" (sin cambios) ---

        private async Task LoadDosagesAsync()
        {
            Dosages.Clear();
            var pautas = await _dosageRepository.GetAllActiveAsync();
            foreach (var p in pautas) Dosages.Add(p);
        }

        private async Task SaveDosageAsync()
        {
            if (string.IsNullOrWhiteSpace(NewDosagePauta)) return;

            var newDosage = new Dosage { Pauta = NewDosagePauta };
            await _dosageRepository.AddAsync(newDosage);
            await _dosageRepository.SaveChangesAsync();

            NewDosagePauta = string.Empty;
            await LoadDosagesAsync();
        }
    }
}