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
                    OnSelectedPatientChanged(value);
                    GeneratePrescriptionPdfCommand.NotifyCanExecuteChanged();
                    GenerateBasicPrescriptionPdfCommand.NotifyCanExecuteChanged(); // <-- AÑADIDO
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
                    GenerateBasicPrescriptionPdfCommand.NotifyCanExecuteChanged(); // <-- AÑADIDO
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
                    GenerateBasicPrescriptionPdfCommand.NotifyCanExecuteChanged(); // <-- AÑADIDO
                }
            }
        }

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
        public IAsyncRelayCommand GeneratePrescriptionPdfCommand { get; }
        public IAsyncRelayCommand GenerateBasicPrescriptionPdfCommand { get; } // <-- AÑADIDO
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

            // Inicialización de comandos
            SelectPatientCommand = new RelayCommand(SelectPatient);
            GeneratePrescriptionPdfCommand = new AsyncRelayCommand(GeneratePrescriptionPdfAsync, CanGeneratePrescription);
            GenerateBasicPrescriptionPdfCommand = new AsyncRelayCommand(GenerateBasicPrescriptionPdfAsync, CanGeneratePrescription); // <-- AÑADIDO
            LoadMedicationsCommand = new AsyncRelayCommand(LoadMedicationsAsync);
            SaveMedicationCommand = new AsyncRelayCommand(SaveMedicationAsync);
            LoadDosagesCommand = new AsyncRelayCommand(LoadDosagesAsync);
            SaveDosageCommand = new AsyncRelayCommand(SaveDosageAsync);


            _ = LoadInitialDataAsync();
        }

        private async Task LoadInitialDataAsync()
        {
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

        // --- INICIO DE LA REFACTORIZACIÓN ---

        /// <summary>
        /// Lógica centralizada para validar, crear y guardar la receta en la BD.
        /// </summary>
        /// <returns>La prescripción guardada, o null si falla la validación o el guardado.</returns>
        private async Task<Prescription?> CreateAndSavePrescriptionAsync()
        {
            // 1. Validar datos
            if (!CanGeneratePrescription())
            {
                _dialogService.ShowMessage("Debe seleccionar un paciente e introducir un medicamento y una pauta.", "Datos incompletos");
                return null;
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
                PrescriptorCollegeNum = currentUser?.CollegeNumber ?? string.Empty,
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
                prescription.Patient = null; // Evitar que EF intente guardar el paciente
                await _prescriptionRepository.AddAsync(prescription);
                await _prescriptionRepository.SaveChangesAsync();
                return prescription; // Devolver la prescripción guardada (con su ID)
            }
            catch (Exception ex)
            {
                string innerExMessage = ex.InnerException?.Message ?? ex.Message;
                _dialogService.ShowMessage($"Error al guardar la receta en la BD: {innerExMessage}", "Error BD");
                return null; // Falló el guardado
            }
        }

        /// <summary>
        /// Método del comando para la receta OFICIAL.
        /// </summary>
        private async Task GeneratePrescriptionPdfAsync()
        {
            // 1. Crear y guardar la prescripción
            var prescription = await CreateAndSavePrescriptionAsync();
            if (prescription == null) return; // Falló la validación o el guardado

            // 2. Generar el PDF específico
            string pdfPath = string.Empty;
            try
            {
                pdfPath = await _pdfService.GeneratePrescriptionPdfAsync(prescription);

                _dialogService.ShowMessage($"PDF de Receta Oficial generado para: {SelectedPatient!.Name}\nGuardado en: {pdfPath}", "Receta Generada");

                // 3. Abrir el PDF
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(pdfPath) { UseShellExecute = true });
            }
            catch (Exception pdfEx)
            {
                _dialogService.ShowMessage($"Error al generar el PDF de la receta:\n{pdfEx.Message}", "Error PDF");
            }

            // 4. Limpiar formulario
            ClearForm();
        }

        /// <summary>
        /// ¡NUEVO! Método del comando para la receta BÁSICA.
        /// </summary>
        private async Task GenerateBasicPrescriptionPdfAsync()
        {
            // 1. Crear y guardar la prescripción (lógica idéntica)
            var prescription = await CreateAndSavePrescriptionAsync();
            if (prescription == null) return; // Falló la validación o el guardado

            // 2. Generar el PDF específico (¡llamando al nuevo método del servicio!)
            string pdfPath = string.Empty;
            try
            {
                pdfPath = await _pdfService.GenerateBasicPrescriptionPdfAsync(prescription);

                _dialogService.ShowMessage($"PDF de Receta Básica generado para: {SelectedPatient!.Name}\nGuardado en: {pdfPath}", "Receta Generada");

                // 3. Abrir el PDF
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(pdfPath) { UseShellExecute = true });
            }
            catch (Exception pdfEx)
            {
                _dialogService.ShowMessage($"Error al generar el PDF de la receta básica:\n{pdfEx.Message}", "Error PDF");
            }

            // 4. Limpiar formulario
            ClearForm();
        }

        /// <summary>
        /// Método helper para limpiar el formulario.
        /// </summary>
        private void ClearForm()
        {
            SelectedPatient = null;
            MedicationSearchText = string.Empty;
            DosageSearchText = string.Empty;
            Instructions = string.Empty;
            MedicationQuantity = "1";
            TreatmentDuration = "10 días";
            SelectedMedicationForPrescription = null; // Limpiar el ComboBox
        }

        // --- FIN DE LA REFACTORIZACIÓN ---


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