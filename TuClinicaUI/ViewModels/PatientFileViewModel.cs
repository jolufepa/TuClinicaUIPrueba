// En: TuClinicaUI/ViewModels/PatientFileViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.UI.Messages;
using TuClinica.UI.Views;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;
using System.Diagnostics;
using System.IO;
using TuClinica.UI.ViewModels.Events;
// --- AÑADIR ESTE USING ---
using TuClinica.Core.Extensions;

namespace TuClinica.UI.ViewModels
{
    public partial class PatientFileViewModel : BaseViewModel
    {
        // --- Servicios ---
        private readonly IDialogService _dialogService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IFileDialogService _fileDialogService;
        private readonly IValidationService _validationService;

        private Patient? _originalPatientState;

        [ObservableProperty]
        private Patient? _currentPatient;
        public ObservableCollection<ToothViewModel> Odontogram { get; } = new();

        [ObservableProperty]
        private ObservableCollection<ClinicalEntry> _visitHistory = new();
        [ObservableProperty]
        private ObservableCollection<Payment> _paymentHistory = new();

        [ObservableProperty]
        private ObservableCollection<HistorialEventBase> _historialCombinado = new();

        [ObservableProperty]
        private decimal _totalCharged;
        [ObservableProperty]
        private decimal _totalPaid;
        [ObservableProperty]
        private decimal _currentBalance;

        [ObservableProperty]
        private ObservableCollection<ClinicalEntry> _pendingCharges = new();
        [ObservableProperty]
        private ObservableCollection<Payment> _unallocatedPayments = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AllocatePaymentCommand))]
        private ClinicalEntry? _selectedCharge;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AllocatePaymentCommand))]
        private Payment? _selectedPayment;
        [ObservableProperty]
        private decimal _amountToAllocate;

        public ObservableCollection<Treatment> AvailableTreatments { get; } = new();

        public ObservableCollection<TreatmentPlanItem> PendingTasks { get; } = new();

        [ObservableProperty]
        private int _pendingTaskCount = 0;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddPlanItemAsyncCommand))]
        private string _newPlanItemDescription = string.Empty;


        public IAsyncRelayCommand<ClinicalEntry> DeleteClinicalEntryAsyncCommand { get; }
        public IRelayCommand ToggleEditPatientDataCommand { get; }
        public IAsyncRelayCommand SavePatientDataAsyncCommand { get; }
        public IAsyncRelayCommand AllocatePaymentCommand { get; }
        public IAsyncRelayCommand RegisterNewPaymentCommand { get; }
        public IAsyncRelayCommand PrintOdontogramCommand { get; }
        public IRelayCommand NewBudgetCommand { get; }
        public IAsyncRelayCommand PrintHistoryCommand { get; }
        public IAsyncRelayCommand OpenRegisterChargeDialogCommand { get; }

        public IAsyncRelayCommand AddPlanItemAsyncCommand { get; }
        public IAsyncRelayCommand<TreatmentPlanItem> TogglePlanItemAsyncCommand { get; }
        public IAsyncRelayCommand<TreatmentPlanItem> DeletePlanItemAsyncCommand { get; }
        public IAsyncRelayCommand CheckPendingTasksCommand { get; }

        // --- AÑADIDO ---
        public IEnumerable<PatientDocumentType> DocumentTypes => Enum.GetValues(typeof(PatientDocumentType)).Cast<PatientDocumentType>();


        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SavePatientDataAsyncCommand))]
        private bool _isPatientDataReadOnly = true;
        [ObservableProperty]
        private OdontogramPreviewViewModel _odontogramPreviewVM = new();
        [ObservableProperty]
        private bool _isLoading = false;

        public PatientFileViewModel(
          IDialogService dialogService,
          IServiceScopeFactory scopeFactory,
          IFileDialogService fileDialogService,
          IValidationService validationService)
        {
            _dialogService = dialogService;
            _scopeFactory = scopeFactory;
            _fileDialogService = fileDialogService;
            _validationService = validationService;

            InitializeOdontogram();
            WeakReferenceMessenger.Default.Register<OpenOdontogramMessage>(this, (r, m) => OpenOdontogramWindow());

            DeleteClinicalEntryAsyncCommand = new AsyncRelayCommand<ClinicalEntry>(DeleteClinicalEntryAsync, CanDeleteClinicalEntry);
            ToggleEditPatientDataCommand = new RelayCommand(ToggleEditPatientData);
            SavePatientDataAsyncCommand = new AsyncRelayCommand(SavePatientDataAsync, CanSavePatientData);
            AllocatePaymentCommand = new AsyncRelayCommand(AllocatePayment, CanAllocate);
            RegisterNewPaymentCommand = new AsyncRelayCommand(RegisterNewPayment);
            PrintOdontogramCommand = new AsyncRelayCommand(PrintOdontogramAsync);
            NewBudgetCommand = new RelayCommand(NewBudget);
            PrintHistoryCommand = new AsyncRelayCommand(PrintHistoryAsync);
            OpenRegisterChargeDialogCommand = new AsyncRelayCommand(OpenRegisterChargeDialog);

            AddPlanItemAsyncCommand = new AsyncRelayCommand(AddPlanItemAsync, CanAddPlanItem);
            TogglePlanItemAsyncCommand = new AsyncRelayCommand<TreatmentPlanItem>(TogglePlanItemAsync);
            DeletePlanItemAsyncCommand = new AsyncRelayCommand<TreatmentPlanItem>(DeletePlanItemAsync);
            CheckPendingTasksCommand = new AsyncRelayCommand(CheckPendingTasksAsync);

            _unallocatedPayments.CollectionChanged += (s, e) => AllocatePaymentCommand.NotifyCanExecuteChanged();
            _pendingCharges.CollectionChanged += (s, e) => AllocatePaymentCommand.NotifyCanExecuteChanged();
        }

        public async Task LoadPatient(Patient patient)
        {
            if (_isLoading) return;
            try
            {
                _isLoading = true;

                SelectedCharge = null;
                SelectedPayment = null;
                AmountToAllocate = 0;

                NewPlanItemDescription = string.Empty;

                CurrentPatient = patient;
                IsPatientDataReadOnly = true;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var clinicalEntryRepo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();
                    var paymentRepo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
                    var treatmentRepo = scope.ServiceProvider.GetRequiredService<ITreatmentRepository>();
                    var planItemRepo = scope.ServiceProvider.GetRequiredService<ITreatmentPlanItemRepository>();

                    var clinicalHistoryTask = clinicalEntryRepo.GetHistoryForPatientAsync(patient.Id);
                    var paymentHistoryTask = paymentRepo.GetPaymentsForPatientAsync(patient.Id);
                    var treatmentsTask = LoadAvailableTreatments(treatmentRepo);
                    var pendingTasksTask = LoadPendingTasksAsync(planItemRepo, patient.Id);

                    await Task.WhenAll(clinicalHistoryTask, paymentHistoryTask, treatmentsTask, pendingTasksTask);

                    var clinicalHistory = (await clinicalHistoryTask).ToList();
                    var paymentHistory = (await paymentHistoryTask).ToList();

                    VisitHistory.Clear();
                    PaymentHistory.Clear();
                    clinicalHistory.ForEach(VisitHistory.Add);
                    paymentHistory.ForEach(PaymentHistory.Add);
                }

                await RefreshBillingCollections();

                LoadOdontogramStateFromJson();
                OdontogramPreviewVM.LoadFromMaster(this.Odontogram);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al cargar ficha: {ex.Message}", "Error");
            }
            finally
            {
                _isLoading = false;
                SavePatientDataAsyncCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task LoadPendingTasksAsync(ITreatmentPlanItemRepository planItemRepo, int patientId)
        {
            PendingTasks.Clear();
            var tasks = await planItemRepo.GetTasksForPatientAsync(patientId);

            foreach (var task in tasks.OrderBy(t => t.IsDone).ThenByDescending(t => t.DateAdded))
            {
                PendingTasks.Add(task);
            }

            PendingTaskCount = PendingTasks.Count(t => !t.IsDone);
        }

        private bool CanAddPlanItem()
        {
            return !string.IsNullOrWhiteSpace(NewPlanItemDescription);
        }

        partial void OnNewPlanItemDescriptionChanged(string value)
        {
            AddPlanItemAsyncCommand.NotifyCanExecuteChanged();
        }

        private async Task AddPlanItemAsync()
        {
            if (CurrentPatient == null || !CanAddPlanItem()) return;

            var newItem = new TreatmentPlanItem
            {
                PatientId = CurrentPatient.Id,
                Description = NewPlanItemDescription,
                IsDone = false,
                DateAdded = DateTime.Now
            };

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var planItemRepo = scope.ServiceProvider.GetRequiredService<ITreatmentPlanItemRepository>();
                    await planItemRepo.AddAsync(newItem);
                    await planItemRepo.SaveChangesAsync();

                    await LoadPendingTasksAsync(planItemRepo, CurrentPatient.Id);
                }
                NewPlanItemDescription = string.Empty;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al guardar la tarea: {ex.Message}", "Error BD");
            }
        }

        private async Task TogglePlanItemAsync(TreatmentPlanItem? item)
        {
            if (item == null || CurrentPatient == null) return;

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var planItemRepo = scope.ServiceProvider.GetRequiredService<ITreatmentPlanItemRepository>();

                    var itemToUpdate = await planItemRepo.GetByIdAsync(item.Id);
                    if (itemToUpdate == null) return;

                    itemToUpdate.IsDone = !itemToUpdate.IsDone;
                    planItemRepo.Update(itemToUpdate);
                    await planItemRepo.SaveChangesAsync();

                    await LoadPendingTasksAsync(planItemRepo, CurrentPatient.Id);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al actualizar la tarea: {ex.Message}", "Error BD");
            }
        }

        private async Task DeletePlanItemAsync(TreatmentPlanItem? item)
        {
            if (item == null || CurrentPatient == null) return;

            var result = _dialogService.ShowConfirmation(
                $"¿Está seguro de que desea eliminar esta tarea?\n\n'{item.Description}'",
                "Confirmar Eliminación");

            if (result == CoreDialogResult.No) return;

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var planItemRepo = scope.ServiceProvider.GetRequiredService<ITreatmentPlanItemRepository>();

                    var itemToDelete = await planItemRepo.GetByIdAsync(item.Id);
                    if (itemToDelete == null) return;

                    planItemRepo.Remove(itemToDelete);
                    await planItemRepo.SaveChangesAsync();

                    await LoadPendingTasksAsync(planItemRepo, CurrentPatient.Id);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al eliminar la tarea: {ex.Message}", "Error BD");
            }
        }

        private async Task CheckPendingTasksAsync()
        {
            await Task.Delay(50);

            if (PendingTaskCount == 0)
            {
                return;
            }

            var pendingTaskDescriptions = PendingTasks
                .Where(t => !t.IsDone)
                .Select(t => t.Description);

            string taskList = string.Join("\n- ", pendingTaskDescriptions);

            _dialogService.ShowMessage(
                $"El paciente tiene {PendingTaskCount} tarea(s) pendiente(s):\n\n- {taskList}",
                "Plan de Tratamiento Pendiente");
        }

        partial void OnCurrentPatientChanged(Patient? oldValue, Patient? newValue)
        {
            if (oldValue != null)
            {
                oldValue.PropertyChanged -= CurrentPatient_PropertyChanged;
                oldValue.ErrorsChanged -= CurrentPatient_ErrorsChanged;
            }

            if (newValue != null)
            {
                newValue.PropertyChanged += CurrentPatient_PropertyChanged;
                newValue.ErrorsChanged += CurrentPatient_ErrorsChanged;
                _originalPatientState = newValue.DeepCopy();
            }
            else
            {
                _originalPatientState = null;
            }

            SavePatientDataAsyncCommand.NotifyCanExecuteChanged();
        }

        private void CurrentPatient_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (IsPatientDataReadOnly) return;
            SavePatientDataAsyncCommand.NotifyCanExecuteChanged();
        }

        private void CurrentPatient_ErrorsChanged(object? sender, System.ComponentModel.DataErrorsChangedEventArgs e)
        {
            if (!IsPatientDataReadOnly)
            {
                SavePatientDataAsyncCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task AllocatePayment()
        {
            if (SelectedCharge == null || SelectedPayment == null || AmountToAllocate <= 0) return;

            if (AmountToAllocate > SelectedPayment.UnallocatedAmount)
            {
                _dialogService.ShowMessage("La cantidad a asignar supera el monto no asignado del pago.", "Error");
                return;
            }
            if (AmountToAllocate > SelectedCharge.Balance)
            {
                _dialogService.ShowMessage("La cantidad a asignar supera el saldo pendiente del cargo.", "Error");
                return;
            }

            var allocation = new PaymentAllocation
            {
                PaymentId = SelectedPayment.Id,
                ClinicalEntryId = SelectedCharge.Id,
                AmountAllocated = AmountToAllocate
            };

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var allocationRepo = scope.ServiceProvider.GetRequiredService<IRepository<PaymentAllocation>>();
                    await allocationRepo.AddAsync(allocation);
                    await allocationRepo.SaveChangesAsync();
                }

                await RefreshBillingCollections();

                SelectedCharge = null;
                SelectedPayment = null;
                AmountToAllocate = 0;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al asignar el pago: {ex.Message}", "Error BD");
            }
        }
        private bool CanAllocate()
        {
            return SelectedCharge != null && SelectedPayment != null && AmountToAllocate > 0;
        }

        private async Task RefreshBillingCollections()
        {
            if (CurrentPatient == null) return;

            List<ClinicalEntry> clinicalHistory;
            List<Payment> paymentHistory;

            using (var scope = _scopeFactory.CreateScope())
            {
                var clinicalEntryRepo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();
                var paymentRepo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

                var clinicalHistoryTask = clinicalEntryRepo.GetHistoryForPatientAsync(CurrentPatient.Id);
                var paymentHistoryTask = paymentRepo.GetPaymentsForPatientAsync(CurrentPatient.Id);
                await Task.WhenAll(clinicalHistoryTask, paymentHistoryTask);

                clinicalHistory = (await clinicalHistoryTask).ToList();
                paymentHistory = (await paymentHistoryTask).ToList();

                VisitHistory.Clear();
                PaymentHistory.Clear();
                clinicalHistory.ForEach(VisitHistory.Add);
                paymentHistory.ForEach(PaymentHistory.Add);
            }

            TotalCharged = VisitHistory.Sum(c => c.TotalCost);
            TotalPaid = PaymentHistory.Sum(p => p.Amount);
            CurrentBalance = TotalCharged - TotalPaid;

            PendingCharges.Clear();
            VisitHistory.Where(c => c.Balance > 0).OrderBy(c => c.VisitDate).ToList().ForEach(PendingCharges.Add);
            UnallocatedPayments.Clear();
            PaymentHistory.Where(p => p.UnallocatedAmount > 0).OrderBy(p => p.PaymentDate).ToList().ForEach(UnallocatedPayments.Add);

            HistorialCombinado.Clear();

            foreach (var cargo in clinicalHistory)
            {
                HistorialCombinado.Add(new CargoEvent(cargo, this));
            }
            foreach (var abono in paymentHistory)
            {
                HistorialCombinado.Add(new AbonoEvent(abono));
            }

            var sortedList = HistorialCombinado.OrderByDescending(e => e.Timestamp).ToList();

            HistorialCombinado.Clear();
            foreach (var item in sortedList)
            {
                HistorialCombinado.Add(item);
            }
        }

        private void NewBudget()
        {
            if (CurrentPatient == null) return;
            WeakReferenceMessenger.Default.Send(new NavigateToNewBudgetMessage(CurrentPatient));
        }

        private async Task PrintHistoryAsync()
        {
            if (CurrentPatient == null)
            {
                _dialogService.ShowMessage("No hay un paciente cargado.", "Error");
                return;
            }

            _dialogService.ShowMessage("Generando el informe PDF del historial... Por favor, espere.", "Generando PDF");

            try
            {
                byte[] pdfBytes;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var pdfService = scope.ServiceProvider.GetRequiredService<IPdfService>();

                    pdfBytes = await pdfService.GenerateHistoryPdfAsync(
                        CurrentPatient,
                        VisitHistory.ToList(),
                        PaymentHistory.ToList(),
                        CurrentBalance
                    );
                }

                string tempFileName = $"Historial_{CurrentPatient.Surname}_{CurrentPatient.Name}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                string tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);

                await File.WriteAllBytesAsync(tempFilePath, pdfBytes);

                Process.Start(new ProcessStartInfo(tempFilePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Se produjo un error al generar o abrir el PDF del historial:\n{ex.Message}", "Error de Impresión");
            }
        }

        private async Task PrintOdontogramAsync()
        {
            if (CurrentPatient == null) return;

            try
            {
                string jsonState = JsonSerializer.Serialize(this.Odontogram);
                string generatedFilePath;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var pdfService = scope.ServiceProvider.GetRequiredService<IPdfService>();
                    generatedFilePath = await pdfService.GenerateOdontogramPdfAsync(CurrentPatient, jsonState);
                }

                var result = _dialogService.ShowConfirmation(
                    $"PDF del odontograma generado con éxito en:\n{generatedFilePath}\n\n¿Desea abrir el archivo ahora?",
                    "Éxito");

                if (result == CoreDialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(generatedFilePath) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al generar el PDF del odontograma:\n{ex.Message}", "Error de Impresión");
            }
        }

        private async Task RegisterNewPayment()
        {
            if (CurrentPatient == null) return;

            var (ok, amount, method, observaciones, date) = _dialogService.ShowNewPaymentDialog();
            if (!ok || amount <= 0) return;

            var newPayment = new Payment
            {
                PatientId = CurrentPatient.Id,
                PaymentDate = date ?? DateTime.Now,
                Amount = amount,
                Method = method,
                Observaciones = observaciones
            };

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var paymentRepo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
                    await paymentRepo.AddAsync(newPayment);
                    await paymentRepo.SaveChangesAsync();
                }

                await RefreshBillingCollections();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al guardar el pago: {ex.Message}", "Error BD");
            }
        }

        private void InitializeOdontogram()
        {
            Odontogram.Clear();
            for (int i = 18; i >= 11; i--) Odontogram.Add(new ToothViewModel(i));
            for (int i = 21; i <= 28; i++) Odontogram.Add(new ToothViewModel(i));
            for (int i = 41; i <= 48; i++) Odontogram.Add(new ToothViewModel(i));
            for (int i = 38; i >= 31; i--) Odontogram.Add(new ToothViewModel(i));
        }

        private void LoadOdontogramStateFromJson()
        {
            foreach (var tooth in Odontogram)
            {
                tooth.FullCondition = ToothCondition.Sano;
                tooth.OclusalCondition = ToothCondition.Sano;
                tooth.MesialCondition = ToothCondition.Sano;
                tooth.DistalCondition = ToothCondition.Sano;
                tooth.VestibularCondition = ToothCondition.Sano;
                tooth.LingualCondition = ToothCondition.Sano;
                tooth.FullRestoration = ToothRestoration.Ninguna;
                tooth.OclusalRestoration = ToothRestoration.Ninguna;
                tooth.MesialRestoration = ToothRestoration.Ninguna;
                tooth.DistalRestoration = ToothRestoration.Ninguna;
                tooth.VestibularRestoration = ToothRestoration.Ninguna;
                tooth.LingualRestoration = ToothRestoration.Ninguna;
            }

            if (CurrentPatient == null || string.IsNullOrWhiteSpace(CurrentPatient.OdontogramStateJson))
            {
                return;
            }

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var savedState = JsonSerializer.Deserialize<List<ToothViewModel>>(CurrentPatient.OdontogramStateJson, options);

                if (savedState == null) return;

                foreach (var savedTooth in savedState)
                {
                    var masterTooth = Odontogram.FirstOrDefault(t => t.ToothNumber == savedTooth.ToothNumber);
                    if (masterTooth != null)
                    {
                        masterTooth.FullCondition = savedTooth.FullCondition;
                        masterTooth.OclusalCondition = savedTooth.OclusalCondition;
                        masterTooth.MesialCondition = savedTooth.MesialCondition;
                        masterTooth.DistalCondition = savedTooth.DistalCondition;
                        masterTooth.VestibularCondition = savedTooth.VestibularCondition;
                        masterTooth.LingualCondition = savedTooth.LingualCondition;

                        masterTooth.FullRestoration = savedTooth.FullRestoration;
                        masterTooth.OclusalRestoration = savedTooth.OclusalRestoration;
                        masterTooth.MesialRestoration = savedTooth.MesialRestoration;
                        masterTooth.DistalRestoration = savedTooth.DistalRestoration;
                        masterTooth.VestibularRestoration = savedTooth.VestibularRestoration;
                        masterTooth.LingualRestoration = savedTooth.LingualRestoration;
                    }
                }
            }
            catch (JsonException ex)
            {
                _dialogService.ShowMessage($"Error al leer el estado del odontograma guardado (JSON corrupto): {ex.Message}\nSe cargará un odontograma vacío.", "Error Odontograma");
            }
        }

        private async void OpenOdontogramWindow()
        {
            if (CurrentPatient == null)
            {
                _dialogService.ShowMessage("Debe tener un paciente cargado para abrir el odontograma.", "Error");
                return;
            }
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var vm = scope.ServiceProvider.GetRequiredService<OdontogramViewModel>();
                    var dialog = scope.ServiceProvider.GetRequiredService<OdontogramWindow>();

                    vm.LoadState(this.Odontogram, this.CurrentPatient);

                    dialog.DataContext = vm;

                    Window? owner = Application.Current.MainWindow;
                    if (owner != null && owner != dialog)
                    {
                        dialog.Owner = owner;
                    }

                    vm.DialogResult = null;
                    dialog.ShowDialog();


                    if (vm.DialogResult == true)
                    {
                        var newJsonState = vm.GetSerializedState();
                        if (CurrentPatient.OdontogramStateJson != newJsonState)
                        {
                            CurrentPatient.OdontogramStateJson = newJsonState;
                            await SavePatientOdontogramStateAsync();
                        }
                    }
                }

                LoadOdontogramStateFromJson();
                OdontogramPreviewVM.LoadFromMaster(this.Odontogram);

            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al abrir el odontograma: {ex.Message}", "Error");
            }
        }

        private async Task SavePatientOdontogramStateAsync()
        {
            if (CurrentPatient == null) return;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var patientRepo = scope.ServiceProvider.GetRequiredService<IPatientRepository>();
                    var patientToUpdate = await patientRepo.GetByIdAsync(CurrentPatient.Id);
                    if (patientToUpdate != null)
                    {
                        patientToUpdate.OdontogramStateJson = CurrentPatient.OdontogramStateJson;
                        await patientRepo.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al guardar el estado del odontograma: {ex.Message}", "Error BD");
            }
        }

        private bool CanDeleteClinicalEntry(ClinicalEntry? entry)
        {
            return entry != null;
        }

        partial void OnSelectedChargeChanged(ClinicalEntry? value)
        {
            AutoFillAmountToAllocate();
        }

        partial void OnSelectedPaymentChanged(Payment? value)
        {
            AutoFillAmountToAllocate();
        }

        private void AutoFillAmountToAllocate()
        {
            if (SelectedCharge != null && SelectedPayment != null)
            {
                AmountToAllocate = Math.Min(SelectedCharge.Balance, SelectedPayment.UnallocatedAmount);
            }
            else
            {
                AmountToAllocate = 0;
            }
        }

        private void ToggleEditPatientData()
        {
            IsPatientDataReadOnly = !IsPatientDataReadOnly;

            if (!IsPatientDataReadOnly) // Entrando en modo edición
            {
                if (CurrentPatient != null)
                {
                    _originalPatientState = CurrentPatient.DeepCopy();

                    // ¡AQUÍ ESTÁ LA CORRECCIÓN!
                    // Forzamos la validación del modelo en cuanto se pulsa "Editar".
                    CurrentPatient.ForceValidation();
                }
            }
            else // Cancelando el modo edición
            {
                if (CurrentPatient != null && _originalPatientState != null)
                {
                    CurrentPatient.CopyFrom(_originalPatientState);
                }
                _originalPatientState = null;
            }
            SavePatientDataAsyncCommand.NotifyCanExecuteChanged();
        }

        private async Task OpenRegisterChargeDialog()
        {
            using (var dbScope = _scopeFactory.CreateScope())
            {
                var authService = dbScope.ServiceProvider.GetRequiredService<IAuthService>();

                if (CurrentPatient == null || authService.CurrentUser == null) return;

                var (ok, data) = _dialogService.ShowManualChargeDialog(this.AvailableTreatments);

                if (ok && data != null)
                {
                    string concept = data.Concept;
                    decimal unitPrice = data.UnitPrice;
                    int quantity = data.Quantity;
                    int? treatmentId = data.TreatmentId;
                    string observaciones = data.Observaciones;
                    DateTime visitDate = data.SelectedDate ?? DateTime.Now;

                    decimal totalCost = unitPrice * quantity;

                    try
                    {
                        var clinicalRepo = dbScope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();

                        var clinicalEntry = new ClinicalEntry
                        {
                            PatientId = CurrentPatient.Id,
                            DoctorId = authService.CurrentUser.Id,
                            VisitDate = visitDate,
                            Diagnosis = quantity > 1 ? $"{concept} (x{quantity})" : concept,
                            TotalCost = totalCost,
                            Notes = observaciones
                        };

                        if (treatmentId.HasValue)
                        {
                            clinicalEntry.TreatmentsPerformed.Add(new ToothTreatment
                            {
                                ToothNumber = 0,
                                Surfaces = ToothSurface.Completo,
                                TreatmentId = treatmentId.Value,
                                TreatmentPerformed = ToothRestoration.Ninguna,
                                Price = totalCost
                            });
                        }

                        await clinicalRepo.AddAsync(clinicalEntry);
                        await clinicalRepo.SaveChangesAsync();
                        await RefreshBillingCollections();

                        _dialogService.ShowMessage($"Cargo registrado con éxito:\n\nConcepto: {clinicalEntry.Diagnosis}\nTotal: {totalCost:C}", "Cargo Registrado");
                    }
                    catch (Exception ex)
                    {
                        _dialogService.ShowMessage($"Error al registrar el cargo: {ex.Message}", "Error BD");
                    }
                }
            }
        }

        private async Task LoadAvailableTreatments(ITreatmentRepository treatmentRepository)
        {
            AvailableTreatments.Clear();
            var treatments = await treatmentRepository.GetAllAsync();
            foreach (var treatment in treatments.Where(t => t.IsActive).OrderBy(t => t.Name))
            {
                AvailableTreatments.Add(treatment);
            }
        }

        private bool CanSavePatientData()
        {
            return !IsPatientDataReadOnly &&
                   HasPatientDataChanged() &&
                   CurrentPatient != null &&
                   !CurrentPatient.HasErrors;
        }

        private bool HasPatientDataChanged()
        {
            if (_originalPatientState == null || CurrentPatient == null) return false;

            // --- INICIO DE LA MODIFICACIÓN ---
            return _originalPatientState.Name != CurrentPatient.Name ||
                   _originalPatientState.Surname != CurrentPatient.Surname ||
                   _originalPatientState.DocumentType != CurrentPatient.DocumentType || // <-- AÑADIDO
                   _originalPatientState.DocumentNumber != CurrentPatient.DocumentNumber || // <-- CAMBIADO
                   _originalPatientState.DateOfBirth != CurrentPatient.DateOfBirth ||
                   _originalPatientState.Phone != CurrentPatient.Phone ||
                   _originalPatientState.Address != CurrentPatient.Address ||
                   _originalPatientState.Email != CurrentPatient.Email ||
                   _originalPatientState.Notes != CurrentPatient.Notes;
            // --- FIN DE LA MODIFICACIÓN ---
        }

        private async Task SavePatientDataAsync()
        {
            if (CurrentPatient == null) return;

            CurrentPatient.ForceValidation();

            if (CurrentPatient.HasErrors)
            {
                var firstError = CurrentPatient.GetErrors().FirstOrDefault()?.ErrorMessage;
                _dialogService.ShowMessage($"No se pueden guardar los cambios. Revise los errores.\n\nError: {firstError}", "Datos Inválidos");
                return;
            }

            CurrentPatient.Name = CurrentPatient.Name.ToTitleCase();
            CurrentPatient.Surname = CurrentPatient.Surname.ToTitleCase();

            // --- INICIO DE LA MODIFICACIÓN ---
            CurrentPatient.DocumentNumber = CurrentPatient.DocumentNumber?.ToUpper().Trim() ?? string.Empty;
            CurrentPatient.Email = CurrentPatient.Email?.ToLower(); // El trim ya se hizo en el setter

            if (string.IsNullOrEmpty(CurrentPatient.Email))
            {
                CurrentPatient.Email = null;
            }

            // Validación de Documento
            if (!_validationService.IsValidDocument(CurrentPatient.DocumentNumber, CurrentPatient.DocumentType))
            {
                _dialogService.ShowMessage("El número de documento introducido no tiene un formato válido para el tipo seleccionado.", "Documento Inválido");
                return;
            }
            // --- FIN DE LA MODIFICACIÓN ---

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var patientRepo = scope.ServiceProvider.GetRequiredService<IPatientRepository>();
                    var patientToUpdate = await patientRepo.GetByIdAsync(CurrentPatient.Id);
                    if (patientToUpdate != null)
                    {
                        patientToUpdate.CopyFrom(CurrentPatient);

                        await patientRepo.SaveChangesAsync();
                        _dialogService.ShowMessage("Datos del paciente actualizados.", "Éxito");
                        _originalPatientState = CurrentPatient.DeepCopy();
                        IsPatientDataReadOnly = true;
                    }
                    else
                    {
                        _dialogService.ShowMessage("Error: No se encontró el paciente para actualizar.", "Error");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al guardar los datos del paciente:\n{ex.Message}", "Error Base de Datos");
            }
            finally
            {
                SavePatientDataAsyncCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task DeleteClinicalEntryAsync(ClinicalEntry? SelectedHistoryEntry)
        {
            if (SelectedHistoryEntry == null || CurrentPatient == null) return;
            var result = _dialogService.ShowConfirmation(
              $"¿Está seguro de que desea eliminar permanentemente este cargo?\n\n" +
              $"Concepto: {SelectedHistoryEntry.Diagnosis}\n" +
              $"Coste: {SelectedHistoryEntry.TotalCost:C}\n\n" +
              $"Cualquier pago asignado a este cargo será des-asignado.",
              "Confirmar Eliminación de Cargo");

            if (result == CoreDialogResult.No) return;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var clinicalEntryRepo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();
                    bool success = await clinicalEntryRepo.DeleteEntryAndAllocationsAsync(SelectedHistoryEntry.Id);
                    if (success)
                    {
                        _dialogService.ShowMessage("Cargo eliminado correctamente.", "Éxito");
                    }
                    else
                    {
                        _dialogService.ShowMessage("No se pudo eliminar el cargo (quizás ya estaba borrado).", "Error");
                    }
                }
                await RefreshBillingCollections();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al eliminar el cargo: {ex.Message}", "Error de Base de Datos");
            }
        }
    }
}