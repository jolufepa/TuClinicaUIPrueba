// En: TuClinicaUI/ViewModels/PrescriptionViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.DataAccess;
using TuClinica.UI.Views;
using TuClinica.Core.Interfaces;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;
using TuClinica.Core.Extensions;


namespace TuClinica.UI.ViewModels
{
    public partial class PrescriptionViewModel : BaseViewModel
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IPdfService _pdfService;
        private readonly IMedicationRepository _medicationRepository;
        private readonly IDosageRepository _dosageRepository;
        private readonly IRepository<Prescription> _prescriptionRepository;
        private readonly IDialogService _dialogService;
        private readonly ISettingsService _settingsService;

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

        [ObservableProperty]
        private string _medicationQuantity = "1";

        [ObservableProperty]
        private int _durationInDays = 10;

        [ObservableProperty]
        private string _instructions = string.Empty;


        public ObservableCollection<Medication> Medications { get; set; } = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(EditMedicationCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteMedicationAsyncCommand))]
        private Medication? _selectedMedication;

        [ObservableProperty]
        private string _newMedicationName = string.Empty;

        [ObservableProperty]
        private string _newMedicationPresentation = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormTitle))]
        private bool _isEditingMedication = false;

        public string FormTitle => IsEditingMedication ? "Editar Medicamento" : "Añadir Nuevo Medicamento";

        public ObservableCollection<Dosage> Dosages { get; set; } = new();
        public Dosage? SelectedDosage { get; set; }
        public string NewDosagePauta { get; set; } = string.Empty;

        public IRelayCommand SelectPatientCommand { get; }
        public IAsyncRelayCommand GeneratePrescriptionPdfCommand { get; }
        public IAsyncRelayCommand GenerateBasicPrescriptionPdfCommand { get; }
        public IAsyncRelayCommand LoadMedicationsCommand { get; }
        public IAsyncRelayCommand SaveMedicationAsyncCommand { get; }
        public IAsyncRelayCommand LoadDosagesCommand { get; }
        public IAsyncRelayCommand SaveDosageCommand { get; }

        public IRelayCommand EditMedicationCommand { get; }
        public IAsyncRelayCommand DeleteMedicationAsyncCommand { get; }
        public IRelayCommand ClearMedicationFormCommand { get; }


        public PrescriptionViewModel(
            IServiceScopeFactory scopeFactory,
            IPdfService pdfService,
            IMedicationRepository medicationRepository,
            IDosageRepository dosageRepository,
            IRepository<Prescription> prescriptionRepository,
            IDialogService dialogService,
            ISettingsService settingsService
            )
        {
            _scopeFactory = scopeFactory;
            _pdfService = pdfService;
            _medicationRepository = medicationRepository;
            _dosageRepository = dosageRepository;
            _prescriptionRepository = prescriptionRepository;
            _dialogService = dialogService;
            _settingsService = settingsService;

            SelectPatientCommand = new RelayCommand(SelectPatient);
            GeneratePrescriptionPdfCommand = new AsyncRelayCommand(GeneratePrescriptionPdfAsync, CanGeneratePrescription);
            GenerateBasicPrescriptionPdfCommand = new AsyncRelayCommand(GenerateBasicPrescriptionPdfAsync, CanGeneratePrescription);

            LoadMedicationsCommand = new AsyncRelayCommand(LoadMedicationsAsync);
            SaveMedicationAsyncCommand = new AsyncRelayCommand(SaveMedicationAsync);
            ClearMedicationFormCommand = new RelayCommand(ClearMedicationForm);
            EditMedicationCommand = new RelayCommand(EditMedication, CanEditOrDelete);
            DeleteMedicationAsyncCommand = new AsyncRelayCommand(DeleteMedicationAsync, CanEditOrDelete);

            LoadDosagesCommand = new AsyncRelayCommand(LoadDosagesAsync);
            SaveDosageCommand = new AsyncRelayCommand(SaveDosageAsync);


            _ = LoadInitialDataAsync();
        }

        private async Task LoadInitialDataAsync()
        {
            await LoadMedicationsAsync();
            await LoadDosagesAsync();
        }

        private void SelectPatient()
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dialog = scope.ServiceProvider.GetRequiredService<PatientSelectionDialog>();

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

            AppSettings settings = _settingsService.GetSettings();
            User? currentUser;
            using (var scope = _scopeFactory.CreateScope())
            {
                var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
                currentUser = authService.CurrentUser;
            }

            var prescription = new Prescription
            {
                PatientId = SelectedPatient!.Id,
                Patient = SelectedPatient,
                IssueDate = DateTime.Now,
                Instructions = this.Instructions,
                PrescriptorName = currentUser?.Name ?? settings.ClinicName,
                PrescriptorCollegeNum = currentUser?.CollegeNumber ?? string.Empty,
                PrescriptorSpecialty = currentUser?.Specialty ?? "General",
            };

            var item = new PrescriptionItem
            {
                MedicationName = this.MedicationSearchText,
                DosagePauta = this.DosageSearchText,
                DurationInDays = this.DurationInDays,
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
            DurationInDays = 10;
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

        private async Task LoadMedicationsAsync()
        {
            Medications.Clear();
            var meds = await _medicationRepository.GetAllActiveAsync();
            foreach (var m in meds) Medications.Add(m);
        }

        private bool CanEditOrDelete()
        {
            return SelectedMedication != null;
        }

        private void ClearMedicationForm()
        {
            NewMedicationName = string.Empty;
            NewMedicationPresentation = string.Empty;
            SelectedMedication = null;
            IsEditingMedication = false;
        }

        private void EditMedication()
        {
            if (SelectedMedication == null) return;

            NewMedicationName = SelectedMedication.Name;
            NewMedicationPresentation = SelectedMedication.Presentation ?? string.Empty;
            IsEditingMedication = true;
        }

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
                await LoadMedicationsAsync();
                ClearMedicationForm();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al eliminar el medicamento: {ex.Message}", "Error BD");
            }
        }

        private async Task SaveMedicationAsync()
        {
            string nameToSave = NewMedicationName.ToTitleCase();
            string presentationToSave = NewMedicationPresentation.Trim();

            if (string.IsNullOrWhiteSpace(nameToSave))
            {
                _dialogService.ShowMessage("El nombre del medicamento no puede estar vacío.", "Dato Requerido");
                return;
            }

            try
            {
                if (IsEditingMedication)
                {
                    if (SelectedMedication == null)
                    {
                        _dialogService.ShowMessage("No hay ningún medicamento seleccionado para editar.", "Error");
                        return;
                    }

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
                    var newMed = new Medication
                    {
                        Name = nameToSave,
                        Presentation = presentationToSave
                    };
                    await _medicationRepository.AddAsync(newMed);
                }

                await _medicationRepository.SaveChangesAsync();

                ClearMedicationForm();
                await LoadMedicationsAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al guardar el medicamento: {ex.Message}", "Error BD");
            }
        }

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