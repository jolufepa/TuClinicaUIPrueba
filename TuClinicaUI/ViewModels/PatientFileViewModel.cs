using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TuClinica.Core.Enums;
using TuClinica.Core.Extensions;
using TuClinica.Core.Interfaces;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.DataAccess;
using TuClinica.UI.Messages;
using TuClinica.UI.ViewModels.Events;
using TuClinica.UI.Views;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;

namespace TuClinica.UI.ViewModels
{
    public partial class PatientFileViewModel : BaseViewModel
    {
        // --- Servicios ---
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IFileDialogService _fileDialogService;
        private readonly IValidationService _validationService;
        private readonly IPatientAlertRepository _alertRepository;
        private readonly IPdfService _pdfService;

        private CancellationTokenSource? _loadPatientCts;
        private Patient? _originalPatientState;

        [ObservableProperty]
        private Patient? _currentPatient;

        // --- Colecciones ---
        public ObservableCollection<ToothViewModel> Odontogram { get; } = new();
        public ObservableCollection<DentalConnector> MasterConnectors { get; } = new();

        [ObservableProperty]
        private ObservableCollection<ClinicalEntry> _visitHistory = new();
        [ObservableProperty]
        private ObservableCollection<Payment> _paymentHistory = new();
        [ObservableProperty]
        private ObservableCollection<HistorialEventBase> _historialCombinado = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasActiveAlerts))]
        private ObservableCollection<PatientAlert> _activeAlerts = new();
        public bool HasActiveAlerts => ActiveAlerts.Any();

        [ObservableProperty]
        private string _newAlertMessage = string.Empty;
        [ObservableProperty]
        private AlertLevel _newAlertLevel = AlertLevel.Warning;

        // --- Propiedades de Facturación ---
        [ObservableProperty] private decimal _totalCharged;
        [ObservableProperty] private decimal _totalPaid;
        [ObservableProperty] private decimal _currentBalance;

        [ObservableProperty] private ObservableCollection<ClinicalEntry> _pendingCharges = new();
        [ObservableProperty] private ObservableCollection<Payment> _unallocatedPayments = new();

        [ObservableProperty] private ClinicalEntry? _selectedCharge;
        [ObservableProperty] private Payment? _selectedPayment;
        [ObservableProperty] private decimal _amountToAllocate;

        public ObservableCollection<Treatment> AvailableTreatments { get; } = new();

        // --- Propiedades de Plan de Tratamiento ---
        public ObservableCollection<TreatmentPlanItem> PendingTasks { get; } = new();
        [ObservableProperty] private int _pendingTaskCount = 0;
        [ObservableProperty] private string _newPlanItemDescription = string.Empty;

        // --- Propiedades de Documentos Vinculados ---
        [ObservableProperty] private ObservableCollection<LinkedDocument> _linkedDocuments = new();
        [ObservableProperty] private LinkedDocument? _selectedLinkedDocument;
        [ObservableProperty] private bool _canManageDocuments = false;

        // --- Comandos ---
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
        public IAsyncRelayCommand AddLinkedDocumentCommand { get; }
        public IAsyncRelayCommand DeleteLinkedDocumentCommand { get; }
        public IAsyncRelayCommand AddAlertCommand { get; }
        public IAsyncRelayCommand<PatientAlert> DeleteAlertCommand { get; }

        public IEnumerable<PatientDocumentType> DocumentTypes => Enum.GetValues(typeof(PatientDocumentType)).Cast<PatientDocumentType>();
        public IEnumerable<AlertLevel> AlertLevels => Enum.GetValues(typeof(AlertLevel)).Cast<AlertLevel>();

        [ObservableProperty] private bool _isPatientDataReadOnly = true;
        [ObservableProperty] private OdontogramPreviewViewModel _odontogramPreviewVM = new();
        [ObservableProperty] private bool _isLoading = false;

        public PatientFileViewModel(
          IAuthService authService,
          IDialogService dialogService,
          IServiceScopeFactory scopeFactory,
          IFileDialogService fileDialogService,
          IValidationService validationService,
          IPatientAlertRepository alertRepository,
          IPdfService pdfService)
        {
            _authService = authService;
            _dialogService = dialogService;
            _scopeFactory = scopeFactory;
            _fileDialogService = fileDialogService;
            _validationService = validationService;
            _alertRepository = alertRepository;
            _pdfService = pdfService;

            var user = _authService.CurrentUser;
            if (user != null && (user.Role == UserRole.Administrador || user.Role == UserRole.Doctor))
            {
                CanManageDocuments = true;
            }

            InitializeOdontogram();
            WeakReferenceMessenger.Default.Register<OpenOdontogramMessage>(this, (r, m) => OpenOdontogramWindow());

            // Inicializar Comandos
            DeleteClinicalEntryAsyncCommand = new AsyncRelayCommand<ClinicalEntry>(DeleteClinicalEntryAsync, CanDeleteClinicalEntry);
            ToggleEditPatientDataCommand = new RelayCommand(ToggleEditPatientData);
            SavePatientDataAsyncCommand = new AsyncRelayCommand(SavePatientDataAsync, CanSavePatientData);
            AllocatePaymentCommand = new AsyncRelayCommand(AllocatePayment, CanAllocate); // CS0103 Resuelto: Método definido abajo
            RegisterNewPaymentCommand = new AsyncRelayCommand(RegisterNewPayment);
            PrintOdontogramCommand = new AsyncRelayCommand(PrintOdontogramAsync);
            NewBudgetCommand = new RelayCommand(NewBudget);
            PrintHistoryCommand = new AsyncRelayCommand(PrintHistoryAsync); // CS0103 Resuelto: Método definido abajo
            OpenRegisterChargeDialogCommand = new AsyncRelayCommand(OpenRegisterChargeDialog);
            AddPlanItemAsyncCommand = new AsyncRelayCommand(AddPlanItemAsync, CanAddPlanItem);
            TogglePlanItemAsyncCommand = new AsyncRelayCommand<TreatmentPlanItem>(TogglePlanItemAsync);
            DeletePlanItemAsyncCommand = new AsyncRelayCommand<TreatmentPlanItem>(DeletePlanItemAsync);
            CheckPendingTasksCommand = new AsyncRelayCommand(CheckPendingTasksAsync);
            AddLinkedDocumentCommand = new AsyncRelayCommand(AddLinkedDocumentAsync);
            DeleteLinkedDocumentCommand = new AsyncRelayCommand(DeleteLinkedDocumentAsync, () => SelectedLinkedDocument != null);
            AddAlertCommand = new AsyncRelayCommand(AddAlertAsync, CanAddAlert);
            DeleteAlertCommand = new AsyncRelayCommand<PatientAlert>(DeleteAlertAsync);

            _unallocatedPayments.CollectionChanged += (s, e) => AllocatePaymentCommand.NotifyCanExecuteChanged();
            _pendingCharges.CollectionChanged += (s, e) => AllocatePaymentCommand.NotifyCanExecuteChanged();
        }

        // --- Métodos Parciales de Notificación ---
        partial void OnSelectedLinkedDocumentChanged(LinkedDocument? value) => DeleteLinkedDocumentCommand.NotifyCanExecuteChanged();
        partial void OnNewPlanItemDescriptionChanged(string value) => AddPlanItemAsyncCommand.NotifyCanExecuteChanged();
        partial void OnIsPatientDataReadOnlyChanged(bool value) => SavePatientDataAsyncCommand.NotifyCanExecuteChanged();
        partial void OnSelectedChargeChanged(ClinicalEntry? value) { AutoFillAmountToAllocate(); AllocatePaymentCommand.NotifyCanExecuteChanged(); }
        partial void OnSelectedPaymentChanged(Payment? value) { AutoFillAmountToAllocate(); AllocatePaymentCommand.NotifyCanExecuteChanged(); }
        partial void OnAmountToAllocateChanged(decimal value) => AllocatePaymentCommand.NotifyCanExecuteChanged();
        partial void OnNewAlertMessageChanged(string value) => AddAlertCommand.NotifyCanExecuteChanged();

        // --- Carga de Datos ---
        public async Task LoadPatient(Patient patient)
        {
            _loadPatientCts?.Cancel();
            _loadPatientCts = new CancellationTokenSource();
            var token = _loadPatientCts.Token;

            if (IsLoading) return;

            try
            {
                IsLoading = true;
                SelectedCharge = null; SelectedPayment = null; AmountToAllocate = 0;
                NewPlanItemDescription = ""; NewAlertMessage = "";
                VisitHistory.Clear(); PaymentHistory.Clear(); HistorialCombinado.Clear();
                PendingTasks.Clear(); ActiveAlerts.Clear(); LinkedDocuments.Clear();
                CurrentPatient = null;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var freshPatient = await context.Patients
                                        .Include(p => p.LinkedDocuments)
                                        .AsNoTracking()
                                        .FirstOrDefaultAsync(p => p.Id == patient.Id, token);

                    if (freshPatient == null) return;
                    token.ThrowIfCancellationRequested();

                    CurrentPatient = freshPatient;

                    var planItemRepo = scope.ServiceProvider.GetRequiredService<ITreatmentPlanItemRepository>();
                    var treatmentRepo = scope.ServiceProvider.GetRequiredService<ITreatmentRepository>();
                    var alertRepo = scope.ServiceProvider.GetRequiredService<IPatientAlertRepository>();

                    var treatmentsTask = LoadAvailableTreatments(treatmentRepo, token);
                    var pendingTasksTask = LoadPendingTasksAsync(planItemRepo, CurrentPatient.Id, token);
                    var alertsTask = LoadAlertsAsync(alertRepo, CurrentPatient.Id, token);

                    await Task.WhenAll(treatmentsTask, pendingTasksTask, alertsTask);

                    token.ThrowIfCancellationRequested();

                    if (CurrentPatient.LinkedDocuments != null)
                    {
                        foreach (var doc in CurrentPatient.LinkedDocuments.OrderBy(d => d.DocumentType).ThenBy(d => d.DocumentNumber))
                        {
                            LinkedDocuments.Add(doc);
                        }
                    }
                }

                IsPatientDataReadOnly = true;
                await RefreshBillingCollections(token);
                token.ThrowIfCancellationRequested();

                LoadOdontogramStateFromJson();

                OdontogramPreviewVM.LoadFromMaster(this.Odontogram, this.MasterConnectors);
                ShowCriticalAlertsPopup();
            }
            catch (OperationCanceledException)
            {
                CurrentPatient = null;
                ActiveAlerts.Clear();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al cargar ficha: {ex.Message}", "Error");
            }
            finally
            {
                IsLoading = false;
                SavePatientDataAsyncCommand.NotifyCanExecuteChanged();
            }
        }

        private void InitializeOdontogram()
        {
            Odontogram.Clear();
            for (int i = 18; i >= 11; i--) Odontogram.Add(new ToothViewModel(i));
            for (int i = 21; i <= 28; i++) Odontogram.Add(new ToothViewModel(i));
            for (int i = 41; i <= 48; i++) Odontogram.Add(new ToothViewModel(i));
            for (int i = 38; i >= 31; i--) Odontogram.Add(new ToothViewModel(i));

            MasterConnectors.Clear();
        }

        private void LoadOdontogramStateFromJson()
        {
            // 1. Resetear estado visual
            foreach (var t in Odontogram)
            {
                t.FullCondition = ToothCondition.Sano; t.OclusalCondition = ToothCondition.Sano; t.MesialCondition = ToothCondition.Sano;
                t.DistalCondition = ToothCondition.Sano; t.VestibularCondition = ToothCondition.Sano; t.LingualCondition = ToothCondition.Sano;
                t.FullRestoration = ToothRestoration.Ninguna; t.OclusalRestoration = ToothRestoration.Ninguna; t.MesialRestoration = ToothRestoration.Ninguna;
                t.DistalRestoration = ToothRestoration.Ninguna; t.VestibularRestoration = ToothRestoration.Ninguna; t.LingualRestoration = ToothRestoration.Ninguna;
            }
            MasterConnectors.Clear();

            if (CurrentPatient == null || string.IsNullOrWhiteSpace(CurrentPatient.OdontogramStateJson)) return;

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // Usamos el wrapper con los DTOs
                var wrapper = JsonSerializer.Deserialize<OdontogramPersistenceWrapper>(CurrentPatient.OdontogramStateJson, options);

                if (wrapper != null)
                {
                    if (wrapper.Teeth != null)
                        ApplyTeethState(wrapper.Teeth);

                    if (wrapper.Connectors != null)
                    {
                        foreach (var c in wrapper.Connectors) MasterConnectors.Add(c);
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Error JSON: {ex.Message}"); }
        }

        private void ApplyTeethState(List<ToothStateDto> savedTeeth)
        {
            foreach (var s in savedTeeth)
            {
                var m = Odontogram.FirstOrDefault(t => t.ToothNumber == s.ToothNumber);
                if (m != null)
                {
                    m.FullCondition = s.FullCondition;
                    m.OclusalCondition = s.OclusalCondition;
                    m.MesialCondition = s.MesialCondition;
                    m.DistalCondition = s.DistalCondition;
                    m.VestibularCondition = s.VestibularCondition;
                    m.LingualCondition = s.LingualCondition;

                    m.FullRestoration = s.FullRestoration;
                    m.OclusalRestoration = s.OclusalRestoration;
                    m.MesialRestoration = s.MesialRestoration;
                    m.DistalRestoration = s.DistalRestoration;
                    m.VestibularRestoration = s.VestibularRestoration;
                    m.LingualRestoration = s.LingualRestoration;
                }
            }
        }

        private async void OpenOdontogramWindow()
        {
            if (CurrentPatient == null) { _dialogService.ShowMessage("Sin paciente cargado.", "Error"); return; }
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var vm = scope.ServiceProvider.GetRequiredService<OdontogramViewModel>();
                    var win = scope.ServiceProvider.GetRequiredService<OdontogramWindow>();

                    vm.LoadState(Odontogram, MasterConnectors, CurrentPatient);

                    win.DataContext = vm;
                    if (Application.Current.MainWindow != win) win.Owner = Application.Current.MainWindow;
                    vm.DialogResult = null;

                    win.ShowDialog();

                    if (vm.DialogResult == true)
                    {
                        var json = vm.GetSerializedState();
                        if (CurrentPatient.OdontogramStateJson != json)
                        {
                            CurrentPatient.OdontogramStateJson = json;
                            await SavePatientOdontogramStateAsync();
                            LoadOdontogramStateFromJson();
                        }
                    }
                }
                OdontogramPreviewVM.LoadFromMaster(this.Odontogram, this.MasterConnectors);
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); }
        }

        private async Task SavePatientOdontogramStateAsync()
        {
            if (CurrentPatient == null) return;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IPatientRepository>();
                    var p = await repo.GetByIdAsync(CurrentPatient.Id);
                    if (p != null) { p.OdontogramStateJson = CurrentPatient.OdontogramStateJson; await repo.SaveChangesAsync(); }
                }
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); }
        }

        private async Task SavePatientDataAsync()
        {
            if (CurrentPatient == null || _originalPatientState == null) return;

            CurrentPatient.ForceValidation();
            if (CurrentPatient.HasErrors)
            {
                var firstError = CurrentPatient.GetErrors().FirstOrDefault()?.ErrorMessage;
                _dialogService.ShowMessage($"Error de validación: {firstError}", "Datos Inválidos");
                return;
            }
            CurrentPatient.Name = CurrentPatient.Name.ToTitleCase();
            CurrentPatient.Surname = CurrentPatient.Surname.ToTitleCase();
            CurrentPatient.DocumentNumber = CurrentPatient.DocumentNumber?.ToUpper().Trim() ?? string.Empty;
            CurrentPatient.Email = CurrentPatient.Email?.ToLower().Trim();
            if (string.IsNullOrEmpty(CurrentPatient.Email)) CurrentPatient.Email = null;

            if (!_validationService.IsValidDocument(CurrentPatient.DocumentNumber, CurrentPatient.DocumentType))
            {
                _dialogService.ShowMessage("El número de documento no tiene un formato válido.", "Documento Inválido");
                return;
            }
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    bool docChanged = !string.Equals(_originalPatientState.DocumentNumber, CurrentPatient.DocumentNumber, StringComparison.OrdinalIgnoreCase) ||
                                      _originalPatientState.DocumentType != CurrentPatient.DocumentType;

                    if (docChanged)
                    {
                        var duplicate = await context.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id != CurrentPatient.Id && p.DocumentNumber.ToLower() == CurrentPatient.DocumentNumber.ToLower());
                        if (duplicate == null)
                        {
                            var linkedMatch = await context.LinkedDocuments.AsNoTracking().Include(d => d.Patient).FirstOrDefaultAsync(d => d.PatientId != CurrentPatient.Id && d.DocumentNumber.ToLower() == CurrentPatient.DocumentNumber.ToLower());
                            if (linkedMatch != null) duplicate = linkedMatch.Patient;
                        }

                        if (duplicate != null)
                        {
                            _dialogService.ShowMessage($"El documento ya existe en el paciente: {duplicate.PatientDisplayInfo}", "Duplicado");
                            CurrentPatient.CopyFrom(_originalPatientState);
                            return;
                        }

                        if (!string.IsNullOrWhiteSpace(_originalPatientState.DocumentNumber))
                        {
                            var oldDoc = new LinkedDocument { PatientId = CurrentPatient.Id, DocumentType = _originalPatientState.DocumentType, DocumentNumber = _originalPatientState.DocumentNumber, Notes = $"Archivado el {DateTime.Now:dd/MM/yy}" };
                            context.LinkedDocuments.Add(oldDoc);
                            Application.Current.Dispatcher.Invoke(() => LinkedDocuments.Add(oldDoc));
                        }
                    }

                    var patientToUpdate = await context.Patients.FindAsync(CurrentPatient.Id);
                    if (patientToUpdate != null)
                    {
                        context.Entry(patientToUpdate).CurrentValues.SetValues(CurrentPatient);
                        await context.SaveChangesAsync();
                        _dialogService.ShowMessage("Datos actualizados.", "Éxito");
                        _originalPatientState = CurrentPatient.DeepCopy();
                        IsPatientDataReadOnly = true;
                    }
                }
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error al guardar: {ex.Message}", "Error"); }
        }

        private async Task AddLinkedDocumentAsync()
        {
            if (CurrentPatient == null || !CanManageDocuments) return;
            var (ok, docType, docNum, notes) = _dialogService.ShowLinkedDocumentDialog();
            if (!ok) return;

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IRepository<LinkedDocument>>();
                    var newDoc = new LinkedDocument { PatientId = CurrentPatient.Id, DocumentType = docType, DocumentNumber = docNum, Notes = notes };
                    await repo.AddAsync(newDoc);
                    await repo.SaveChangesAsync();
                    LinkedDocuments.Add(newDoc);
                }
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); }
        }

        private async Task DeleteLinkedDocumentAsync()
        {
            if (SelectedLinkedDocument == null) return;
            if (_dialogService.ShowConfirmation("¿Eliminar documento?", "Confirmar") == CoreDialogResult.No) return;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IRepository<LinkedDocument>>();
                    var doc = await repo.GetByIdAsync(SelectedLinkedDocument.Id);
                    if (doc != null) { repo.Remove(doc); await repo.SaveChangesAsync(); }
                }
                LinkedDocuments.Remove(SelectedLinkedDocument);
                SelectedLinkedDocument = null;
            }
            catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
        }

        // --- Facturación (CS7036 SOLUCIONADO: Método con valor por defecto) ---
        private async Task RefreshBillingCollections(CancellationToken token = default)
        {
            if (CurrentPatient == null) return;
            decimal charged = 0, paid = 0;
            IEnumerable<ClinicalEntry> clinicalHistory = Enumerable.Empty<ClinicalEntry>();
            IEnumerable<Payment> paymentHistory = Enumerable.Empty<Payment>();

            using (var scope = _scopeFactory.CreateScope())
            {
                var cRepo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();
                var pRepo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

                var hTask = cRepo.GetHistoryForPatientAsync(CurrentPatient.Id);
                var pTask = pRepo.GetPaymentsForPatientAsync(CurrentPatient.Id);
                var tcTask = cRepo.GetTotalChargedForPatientAsync(CurrentPatient.Id);
                var tpTask = pRepo.GetTotalPaidForPatientAsync(CurrentPatient.Id);

                await Task.WhenAll(hTask, pTask, tcTask, tpTask);
                token.ThrowIfCancellationRequested();

                clinicalHistory = await hTask;
                paymentHistory = await pTask;
                charged = await tcTask;
                paid = await tpTask;
            }

            VisitHistory.Clear(); PaymentHistory.Clear();
            foreach (var c in clinicalHistory) VisitHistory.Add(c);
            foreach (var p in paymentHistory) PaymentHistory.Add(p);

            TotalCharged = charged; TotalPaid = paid; CurrentBalance = TotalCharged - TotalPaid;

            PendingCharges.Clear();
            foreach (var c in VisitHistory.Where(c => c.Balance > 0).OrderBy(c => c.VisitDate)) PendingCharges.Add(c);

            UnallocatedPayments.Clear();
            foreach (var p in PaymentHistory.Where(p => p.UnallocatedAmount > 0).OrderBy(p => p.PaymentDate)) UnallocatedPayments.Add(p);

            HistorialCombinado.Clear();
            foreach (var c in VisitHistory) HistorialCombinado.Add(new CargoEvent(c, this));
            foreach (var p in PaymentHistory) HistorialCombinado.Add(new AbonoEvent(p));
            var sorted = HistorialCombinado.OrderByDescending(x => x.Timestamp).ToList();
            HistorialCombinado.Clear();
            sorted.ForEach(HistorialCombinado.Add);
        }

        private async Task OpenRegisterChargeDialog()
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
                if (CurrentPatient == null || auth.CurrentUser == null) return;
                var (ok, data) = _dialogService.ShowManualChargeDialog(AvailableTreatments);
                if (ok && data != null)
                {
                    decimal total = data.UnitPrice * data.Quantity;
                    try
                    {
                        var repo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();
                        var entry = new ClinicalEntry
                        {
                            PatientId = CurrentPatient.Id,
                            DoctorId = auth.CurrentUser.Id,
                            VisitDate = data.SelectedDate ?? DateTime.Now,
                            Diagnosis = data.Quantity > 1 ? $"{data.Concept} (x{data.Quantity})" : data.Concept,
                            TotalCost = total,
                            Notes = data.Observaciones
                        };
                        if (data.TreatmentId.HasValue)
                        {
                            entry.TreatmentsPerformed.Add(new ToothTreatment
                            {
                                ToothNumber = 0,
                                Surfaces = ToothSurface.Completo,
                                TreatmentId = data.TreatmentId.Value,
                                TreatmentPerformed = ToothRestoration.Ninguna,
                                Price = total
                            });
                        }
                        await repo.AddAsync(entry); await repo.SaveChangesAsync();
                        await RefreshBillingCollections();
                        _dialogService.ShowMessage("Cargo registrado.", "Éxito");
                    }
                    catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
                }
            }
        }

        private async Task RegisterNewPayment()
        {
            if (CurrentPatient == null) return;
            var (ok, amount, method, obs, date) = _dialogService.ShowNewPaymentDialog();
            if (!ok || amount <= 0) return;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
                    await repo.AddAsync(new Payment { PatientId = CurrentPatient.Id, PaymentDate = date ?? DateTime.Now, Amount = amount, Method = method, Observaciones = obs });
                    await repo.SaveChangesAsync();
                }
                await RefreshBillingCollections();
            }
            catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
        }

        private async Task AllocatePayment()
        {
            if (SelectedCharge == null || SelectedPayment == null || AmountToAllocate <= 0) return;
            if (AmountToAllocate > SelectedPayment.UnallocatedAmount || AmountToAllocate > SelectedCharge.Balance) { _dialogService.ShowMessage("Cantidad inválida.", "Error"); return; }
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IRepository<PaymentAllocation>>();
                    await repo.AddAsync(new PaymentAllocation { PaymentId = SelectedPayment.Id, ClinicalEntryId = SelectedCharge.Id, AmountAllocated = AmountToAllocate });
                    await repo.SaveChangesAsync();
                }
                await RefreshBillingCollections();
                SelectedCharge = null; SelectedPayment = null; AmountToAllocate = 0;
            }
            catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
        }

        // --- Helpers y Otros (CS0103 SOLUCIONADOS: Métodos definidos completamente) ---
        private bool CanAllocate()
        {
            return SelectedCharge != null && SelectedPayment != null && AmountToAllocate > 0;
        }

        private void AutoFillAmountToAllocate() { if (SelectedCharge != null && SelectedPayment != null) AmountToAllocate = Math.Min(SelectedCharge.Balance, SelectedPayment.UnallocatedAmount); else AmountToAllocate = 0; }
        private bool CanDeleteClinicalEntry(ClinicalEntry? e) => e != null;

        private async Task DeleteClinicalEntryAsync(ClinicalEntry? e)
        {
            if (e == null || _dialogService.ShowConfirmation("¿Eliminar cargo?", "Confirmar") == CoreDialogResult.No) return;
            try { using (var s = _scopeFactory.CreateScope()) { await s.ServiceProvider.GetRequiredService<IClinicalEntryRepository>().DeleteEntryAndAllocationsAsync(e.Id); } await RefreshBillingCollections(); }
            catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
        }

        private bool CanAddPlanItem() => !string.IsNullOrWhiteSpace(NewPlanItemDescription);
        private async Task AddPlanItemAsync()
        {
            if (CurrentPatient != null && CanAddPlanItem())
            {
                try { using (var s = _scopeFactory.CreateScope()) { var r = s.ServiceProvider.GetRequiredService<ITreatmentPlanItemRepository>(); await r.AddAsync(new TreatmentPlanItem { PatientId = CurrentPatient.Id, Description = NewPlanItemDescription, DateAdded = DateTime.Now }); await r.SaveChangesAsync(); await LoadPendingTasksAsync(r, CurrentPatient.Id, CancellationToken.None); } NewPlanItemDescription = ""; }
                catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
            }
        }
        private async Task TogglePlanItemAsync(TreatmentPlanItem? i)
        {
            if (i != null) { try { using (var s = _scopeFactory.CreateScope()) { var r = s.ServiceProvider.GetRequiredService<ITreatmentPlanItemRepository>(); var db = await r.GetByIdAsync(i.Id); if (db != null) { db.IsDone = !db.IsDone; r.Update(db); await r.SaveChangesAsync(); await LoadPendingTasksAsync(r, CurrentPatient!.Id, CancellationToken.None); } } } catch { } }
        }
        private async Task DeletePlanItemAsync(TreatmentPlanItem? i)
        {
            if (i != null && _dialogService.ShowConfirmation("¿Borrar?", "Confirmar") == CoreDialogResult.Yes) { try { using (var s = _scopeFactory.CreateScope()) { var r = s.ServiceProvider.GetRequiredService<ITreatmentPlanItemRepository>(); var db = await r.GetByIdAsync(i.Id); if (db != null) { r.Remove(db); await r.SaveChangesAsync(); await LoadPendingTasksAsync(r, CurrentPatient!.Id, CancellationToken.None); } } } catch { } }
        }
        private async Task CheckPendingTasksAsync() { await Task.Delay(50); if (PendingTaskCount > 0) _dialogService.ShowMessage("Hay tareas pendientes.", "Aviso"); }

        private bool CanAddAlert() => !string.IsNullOrWhiteSpace(NewAlertMessage);
        private async Task AddAlertAsync()
        {
            if (CurrentPatient != null && CanAddAlert())
            {
                var a = new PatientAlert { PatientId = CurrentPatient.Id, Message = NewAlertMessage, Level = NewAlertLevel, IsActive = true };
                try { using (var s = _scopeFactory.CreateScope()) { var r = s.ServiceProvider.GetRequiredService<IPatientAlertRepository>(); await r.AddAsync(a); await r.SaveChangesAsync(); } ActiveAlerts.Add(a); NewAlertMessage = ""; } catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
            }
        }
        private async Task DeleteAlertAsync(PatientAlert? a)
        {
            if (a != null && _dialogService.ShowConfirmation("¿Borrar alerta?", "Confirmar") == CoreDialogResult.Yes) { try { using (var s = _scopeFactory.CreateScope()) { var r = s.ServiceProvider.GetRequiredService<IPatientAlertRepository>(); var db = await r.GetByIdAsync(a.Id); if (db != null) { r.Remove(db); await r.SaveChangesAsync(); } } ActiveAlerts.Remove(a); } catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); } }
        }

        private async Task LoadPendingTasksAsync(ITreatmentPlanItemRepository r, int pid, CancellationToken t) { PendingTasks.Clear(); var tasks = await r.GetTasksForPatientAsync(pid); t.ThrowIfCancellationRequested(); foreach (var task in tasks.OrderBy(x => x.IsDone).ThenByDescending(x => x.DateAdded)) PendingTasks.Add(task); PendingTaskCount = PendingTasks.Count(x => !x.IsDone); }
        private async Task LoadAvailableTreatments(ITreatmentRepository r, CancellationToken t) { AvailableTreatments.Clear(); var ts = await r.GetAllAsync(); t.ThrowIfCancellationRequested(); foreach (var tr in ts.Where(x => x.IsActive).OrderBy(x => x.Name)) AvailableTreatments.Add(tr); }
        private async Task LoadAlertsAsync(IPatientAlertRepository r, int pid, CancellationToken t) { ActiveAlerts.Clear(); var als = await r.GetActiveAlertsForPatientAsync(pid, t); foreach (var a in als) ActiveAlerts.Add(a); OnPropertyChanged(nameof(HasActiveAlerts)); }
        private void ShowCriticalAlertsPopup() { if (ActiveAlerts.Any(a => a.Level == AlertLevel.Critical)) { var msg = string.Join("\n", ActiveAlerts.Where(a => a.Level == AlertLevel.Critical).Select(a => "- " + a.Message)); Application.Current.Dispatcher.Invoke(() => _dialogService.ShowMessage($"¡ALERTA CRÍTICA!\n\n{msg}", "ALERTA")); } }

        private bool CanSavePatientData() => !IsPatientDataReadOnly && CurrentPatient != null && !CurrentPatient.HasErrors;
        private void ToggleEditPatientData() { IsPatientDataReadOnly = !IsPatientDataReadOnly; if (!IsPatientDataReadOnly && CurrentPatient != null) { _originalPatientState = CurrentPatient.DeepCopy(); CurrentPatient.ForceValidation(); } else if (CurrentPatient != null && _originalPatientState != null) { CurrentPatient.CopyFrom(_originalPatientState); _originalPatientState = null; } }
        private void NewBudget() { if (CurrentPatient != null) WeakReferenceMessenger.Default.Send(new NavigateToNewBudgetMessage(CurrentPatient)); }

        private async Task PrintHistoryAsync()
        {
            if (CurrentPatient != null)
            {
                try
                {
                    byte[] pdf;
                    using (var s = _scopeFactory.CreateScope())
                    {
                        pdf = await s.ServiceProvider.GetRequiredService<IPdfService>().GenerateHistoryPdfAsync(CurrentPatient, VisitHistory.ToList(), PaymentHistory.ToList(), CurrentBalance);
                    }
                    string p = Path.Combine(Path.GetTempPath(), $"Hist_{DateTime.Now.Ticks}.pdf");
                    await File.WriteAllBytesAsync(p, pdf);
                    Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
                }
                catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
            }
        }

        private async Task PrintOdontogramAsync()
        {
            if (CurrentPatient == null) return;
            try
            {
                var teethDtos = Odontogram.Select(t => new ToothStateDto
                {
                    ToothNumber = t.ToothNumber,
                    FullCondition = t.FullCondition,
                    OclusalCondition = t.OclusalCondition,
                    MesialCondition = t.MesialCondition,
                    DistalCondition = t.DistalCondition,
                    VestibularCondition = t.VestibularCondition,
                    LingualCondition = t.LingualCondition,
                    FullRestoration = t.FullRestoration,
                    OclusalRestoration = t.OclusalRestoration,
                    MesialRestoration = t.MesialRestoration,
                    DistalRestoration = t.DistalRestoration,
                    VestibularRestoration = t.VestibularRestoration,
                    LingualRestoration = t.LingualRestoration
                }).ToList();

                var wrapper = new OdontogramPersistenceWrapper
                {
                    Teeth = teethDtos,
                    Connectors = MasterConnectors.ToList()
                };

                var jsonState = JsonSerializer.Serialize(wrapper);

                string path = await _pdfService.GenerateOdontogramPdfAsync(CurrentPatient, jsonState);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
        }
    }
}