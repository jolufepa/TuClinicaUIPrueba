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

// ELIMINAMOS EL KEYWORD 'partial'

namespace TuClinica.UI.ViewModels
{
    public class PrescriptionViewModel : BaseViewModel
    {
        // Servicios
        private readonly IServiceProvider _serviceProvider;
        private readonly IPdfService _pdfService;
        private readonly IMedicationRepository _medicationRepository;
        private readonly IDosageRepository _dosageRepository;
        private readonly IRepository<Prescription> _prescriptionRepository;
        private readonly IDialogService _dialogService;

        // --- Pestaña "Crear Receta" ---
        private Patient? _selectedPatient;
        public Patient? SelectedPatient
        {
            get => _selectedPatient;
            set
            {
                if (SetProperty(ref _selectedPatient, value))
                {
                    // Llama a la lógica de notificación manual
                    OnSelectedPatientChanged(value);
                    GeneratePrescriptionPdfCommand.NotifyCanExecuteChanged();
                }
            }
        }

        // --- Propiedad Display (versión string para la UI) ---
        private string _selectedPatientFullNameDisplay = "Ningún paciente seleccionado";
        public string SelectedPatientFullNameDisplay
        {
            get => _selectedPatientFullNameDisplay;
            set => SetProperty(ref _selectedPatientFullNameDisplay, value); // ELIMINAR LA PALABRA CLAVE 'private'
        }
        // ----------------------------------------------------

        private string _medicationSearchText = string.Empty;
        public string MedicationSearchText
        {
            get => _medicationSearchText;
            set
            {
                if (SetProperty(ref _medicationSearchText, value))
                {
                    GeneratePrescriptionPdfCommand.NotifyCanExecuteChanged();
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
                }
            }
        }
        // ... (el resto de propiedades simples como Quantity, Duration, Instructions no necesitan el check de CanExecute en el set)

        public string MedicationQuantity { get; set; } = "1";
        public string TreatmentDuration { get; set; } = "10 días";
        public string Instructions { get; set; } = string.Empty;


        // --- Pestaña "Gestionar Medicamentos" ---
        public ObservableCollection<Medication> Medications { get; set; } = new();
        public Medication? SelectedMedication { get; set; }
        public string NewMedicationName { get; set; } = string.Empty;
        public string NewMedicationPresentation { get; set; } = string.Empty;

        // --- Pestaña "Gestionar Pautas" ---
        public ObservableCollection<Dosage> Dosages { get; set; } = new();
        public Dosage? SelectedDosage { get; set; }
        public string NewDosagePauta { get; set; } = string.Empty;

        // --- COMANDOS MANUALES (IRelayCommand) ---
        public IRelayCommand SelectPatientCommand { get; }
        // Generado manualmente para poder usar CanExecute y NotifyCanExecuteChanged
        public IAsyncRelayCommand GeneratePrescriptionPdfCommand { get; }
        public IAsyncRelayCommand LoadMedicationsCommand { get; }
        public IAsyncRelayCommand SaveMedicationCommand { get; }
        public IAsyncRelayCommand LoadDosagesCommand { get; }
        public IAsyncRelayCommand SaveDosageCommand { get; }

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

            // Inicialización de comandos (usando CommunityToolkit.Mvvm.Input.RelayCommand, pero instanciados manualmente)
            SelectPatientCommand = new RelayCommand(SelectPatient);
            GeneratePrescriptionPdfCommand = new AsyncRelayCommand(GeneratePrescriptionPdfAsync, CanGeneratePrescription);
            LoadMedicationsCommand = new AsyncRelayCommand(LoadMedicationsAsync);
            SaveMedicationCommand = new AsyncRelayCommand(SaveMedicationAsync);
            LoadDosagesCommand = new AsyncRelayCommand(LoadDosagesAsync);
            SaveDosageCommand = new AsyncRelayCommand(SaveDosageAsync);


            _ = LoadInitialDataAsync();
            _dialogService = dialogService;
        }

        private async Task LoadInitialDataAsync()
        {
            // Cargar listas para gestión
            await LoadMedicationsAsync();
            await LoadDosagesAsync();
        }

        // --- Lógica Pestaña "Crear Receta" ---

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

        // --- Propiedad para capturar el objeto seleccionado ---
        private Medication? _selectedMedicationForPrescription;
        public Medication? SelectedMedicationForPrescription
        {
            get => _selectedMedicationForPrescription;
            set
            {
                if (SetProperty(ref _selectedMedicationForPrescription, value))
                {
                    // LÓGICA CLAVE: Si se selecciona un objeto, usamos la propiedad FullDisplay para rellenar el campo de texto.
                    if (value != null)
                    {
                        MedicationSearchText = value.FullDisplay; // <--- USAMOS FullDisplay
                    }
                }
            }
        }


        private async Task GeneratePrescriptionPdfAsync()
        {
            // 1. Validar datos
            if (!CanGeneratePrescription())
            {
                _dialogService.ShowMessage("Debe seleccionar un paciente e introducir un medicamento y una pauta.", "Datos incompletos");
                return;
            }

            // 2. Obtener datos del prescriptor (desde AppSettings)
            var settings = _serviceProvider.GetRequiredService<AppSettings>();
            var authService = _serviceProvider.GetRequiredService<IAuthService>();
            var currentUser = authService.CurrentUser;

            // 3. Crear el objeto Prescription
            var prescription = new Prescription
            {
                PatientId = SelectedPatient!.Id,
                Patient = SelectedPatient,
                IssueDate = DateTime.Now,
                Instructions = this.Instructions,
               
                PrescriptorName = currentUser?.Username ?? settings.ClinicName,
                // Usa el nuevo campo CollegeNumber (o un string vacío si es nulo)
                PrescriptorCollegeNum = currentUser?.CollegeNumber ?? string.Empty,
                // Usa el nuevo campo Specialty (o "General" si es nulo)
                PrescriptorSpecialty = currentUser?.Specialty ?? "General",
            };

            // 4. Crear la línea de la receta
            var item = new PrescriptionItem
            {
                MedicationName = this.MedicationSearchText,
                DosagePauta = this.DosageSearchText,
                Duration = this.TreatmentDuration,
                Quantity = this.MedicationQuantity,
                Prescription = prescription
            };
            prescription.Items.Add(item);

            // 5. Guardar la receta en la BD
            try
            {
                prescription.Patient = null;
                await _prescriptionRepository.AddAsync(prescription);
                await _prescriptionRepository.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                string innerExMessage = ex.InnerException?.Message ?? ex.Message;
                _dialogService.ShowMessage($"Error al guardar la receta en la BD: {innerExMessage}", "Error BD");
                return;
            }


            // 6. Generar el PDF
            string pdfPath = string.Empty;
            try
            {
                pdfPath = await _pdfService.GeneratePrescriptionPdfAsync(prescription);

                _dialogService.ShowMessage($"PDF de Receta generado para: {SelectedPatient.Name}\nGuardado en: {pdfPath}", "Receta Generada");

                // Abrir el PDF
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(pdfPath) { UseShellExecute = true });
            }
            catch (Exception pdfEx)
            {
                _dialogService.ShowMessage($"Error al generar el PDF de la receta:\n{pdfEx.Message}", "Error PDF");
            }

            // 7. Limpiar formulario
            SelectedPatient = null;
            MedicationSearchText = string.Empty;
            DosageSearchText = string.Empty;
            Instructions = string.Empty;
            MedicationQuantity = "1";
            TreatmentDuration = "10 días";
        }

        private bool CanGeneratePrescription()
        {
            return SelectedPatient != null &&
                   !string.IsNullOrWhiteSpace(MedicationSearchText) &&
                   !string.IsNullOrWhiteSpace(DosageSearchText);
        }

        // Lógica de notificación de cambio de paciente (reemplaza al método parcial)
        private void OnSelectedPatientChanged(Patient? value)
        {
            // Actualización del nuevo campo de display
            SelectedPatientFullNameDisplay = value?.PatientDisplayInfo ?? "Ningún paciente seleccionado";
        }

        // --- Lógica Pestaña "Gestionar Medicamentos" ---

        private async Task LoadMedicationsAsync()
        {
            Medications.Clear();
            var meds = await _medicationRepository.GetAllActiveAsync();
            foreach (var m in meds) Medications.Add(m);
        }

        private async Task SaveMedicationAsync()
        {
            if (string.IsNullOrWhiteSpace(NewMedicationName)) return;

            var newMed = new Medication
            {
                Name = NewMedicationName,
                Presentation = NewMedicationPresentation
            };
            await _medicationRepository.AddAsync(newMed);
            await _medicationRepository.SaveChangesAsync();

            NewMedicationName = string.Empty;
            NewMedicationPresentation = string.Empty;
            await LoadMedicationsAsync();
        }

        // --- Lógica Pestaña "Gestionar Pautas" ---

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