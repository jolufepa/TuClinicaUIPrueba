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
        private CancellationTokenSource? _loadPatientCts;
        

        private Patient? _originalPatientState;

        [ObservableProperty]
        private Patient? _currentPatient;

        // --- Colecciones ---
        public ObservableCollection<ToothViewModel> Odontogram { get; } = new();

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
        private ClinicalEntry? _selectedCharge;

        [ObservableProperty]
        private Payment? _selectedPayment;

        [ObservableProperty]
        private decimal _amountToAllocate;
        

        public ObservableCollection<Treatment> AvailableTreatments { get; } = new();

        // --- Propiedades de Plan de Tratamiento ---
        public ObservableCollection<TreatmentPlanItem> PendingTasks { get; } = new();

        [ObservableProperty]
        private int _pendingTaskCount = 0;

        // --- INICIO CORRECCIÓN: Eliminar [NotifyCanExecuteChangedFor] ---
        [ObservableProperty]
        private string _newPlanItemDescription = string.Empty;
        

        // --- Propiedades de Documentos Vinculados ---
        [ObservableProperty]
        private ObservableCollection<LinkedDocument> _linkedDocuments = new();

        // --- INICIO CORRECCIÓN: Eliminar [NotifyCanExecuteChangedFor] ---
        [ObservableProperty]
        private LinkedDocument? _selectedLinkedDocument;
        

        [ObservableProperty]
        private bool _canManageDocuments = false;

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


        // --- INICIO CORRECCIÓN: Eliminar [NotifyCanExecuteChangedFor] ---
        [ObservableProperty]
        private bool _isPatientDataReadOnly = true;
        

        [ObservableProperty]
        private OdontogramPreviewViewModel _odontogramPreviewVM = new();

        [ObservableProperty]
        private bool _isLoading = false;

        // --- Constructor ---
        public PatientFileViewModel(
          IAuthService authService,
          IDialogService dialogService,
          IServiceScopeFactory scopeFactory,
          IFileDialogService fileDialogService,
          IValidationService validationService,
          
          IPatientAlertRepository alertRepository // Nueva dependencia
                                                  
          )
        {
            _authService = authService;
            _dialogService = dialogService;
            _scopeFactory = scopeFactory;
            _fileDialogService = fileDialogService;
            _validationService = validationService;
            
            _alertRepository = alertRepository;
            

            // Determinar permisos
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
            AddLinkedDocumentCommand = new AsyncRelayCommand(AddLinkedDocumentAsync);
            DeleteLinkedDocumentCommand = new AsyncRelayCommand(DeleteLinkedDocumentAsync, () => SelectedLinkedDocument != null);
            AddAlertCommand = new AsyncRelayCommand(AddAlertAsync, CanAddAlert);
            DeleteAlertCommand = new AsyncRelayCommand<PatientAlert>(DeleteAlertAsync);

            _unallocatedPayments.CollectionChanged += (s, e) => AllocatePaymentCommand.NotifyCanExecuteChanged();
            _pendingCharges.CollectionChanged += (s, e) => AllocatePaymentCommand.NotifyCanExecuteChanged();
        }

        // --- INICIO CORRECCIÓN: Añadir métodos parciales para notificar cambios de comandos ---
        partial void OnSelectedLinkedDocumentChanged(LinkedDocument? value)
        {
            DeleteLinkedDocumentCommand.NotifyCanExecuteChanged();
        }

        partial void OnNewPlanItemDescriptionChanged(string value)
        {
            AddPlanItemAsyncCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsPatientDataReadOnlyChanged(bool value)
        {
            SavePatientDataAsyncCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedChargeChanged(ClinicalEntry? value)
        {
            AutoFillAmountToAllocate();
            AllocatePaymentCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedPaymentChanged(Payment? value)
        {
            AutoFillAmountToAllocate();
            AllocatePaymentCommand.NotifyCanExecuteChanged();
        }

        partial void OnAmountToAllocateChanged(decimal value)
        {
            AllocatePaymentCommand.NotifyCanExecuteChanged();
        }
        


        // --- Carga de Datos (CON LÓGICA DE CANCELACIÓN Y ALERTAS) ---
        public async Task LoadPatient(Patient patient)
        {
            // Cancelar cualquier carga anterior que aún esté en progreso
            _loadPatientCts?.Cancel();
            _loadPatientCts = new CancellationTokenSource();
            var token = _loadPatientCts.Token;

            if (_isLoading) return; // Evitar re-entrada simple

            try
            {
                _isLoading = true;

                // Limpiar la UI inmediatamente
                SelectedCharge = null;
                SelectedPayment = null;
                AmountToAllocate = 0;
                NewPlanItemDescription = string.Empty;
                NewAlertMessage = string.Empty;
                VisitHistory.Clear();
                PaymentHistory.Clear();
                HistorialCombinado.Clear();
                PendingTasks.Clear();
                ActiveAlerts.Clear();
                LinkedDocuments.Clear();
                CurrentPatient = null; // Limpiar el paciente actual

                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var freshPatient = await context.Patients
                                        .Include(p => p.LinkedDocuments)
                                        .AsNoTracking() // Usar AsNoTracking para la carga inicial
                                        .FirstOrDefaultAsync(p => p.Id == patient.Id, token);

                    if (freshPatient == null) return; // Paciente no encontrado
                    token.ThrowIfCancellationRequested(); // Comprobar si se canceló

                    CurrentPatient = freshPatient;

                    // Cargar el resto de los historiales
                    var planItemRepo = scope.ServiceProvider.GetRequiredService<ITreatmentPlanItemRepository>();
                    var treatmentRepo = scope.ServiceProvider.GetRequiredService<ITreatmentRepository>();
                    var alertRepo = scope.ServiceProvider.GetRequiredService<IPatientAlertRepository>();

                    // Cargar tareas auxiliares en paralelo
                    var treatmentsTask = LoadAvailableTreatments(treatmentRepo, token);
                    var pendingTasksTask = LoadPendingTasksAsync(planItemRepo, CurrentPatient.Id, token);
                    var alertsTask = LoadAlertsAsync(alertRepo, CurrentPatient.Id, token);

                    await Task.WhenAll(treatmentsTask, pendingTasksTask, alertsTask);

                    token.ThrowIfCancellationRequested(); // Comprobar antes de actualizar UI

                    // Llenar la lista de documentos vinculados (ya cargados con el paciente)
                    if (CurrentPatient.LinkedDocuments != null)
                    {
                        foreach (var doc in CurrentPatient.LinkedDocuments.OrderBy(d => d.DocumentType).ThenBy(d => d.DocumentNumber))
                        {
                            LinkedDocuments.Add(doc);
                        }
                    }
                }

                IsPatientDataReadOnly = true;

                // Cargar facturación (con token)
                await RefreshBillingCollections(token);
                token.ThrowIfCancellationRequested();

                // Cargar Odontograma (rápido, sin token)
                LoadOdontogramStateFromJson();
                OdontogramPreviewVM.LoadFromMaster(this.Odontogram);

                // Mostrar Popup de Alertas Críticas (después de que todo ha cargado)
                ShowCriticalAlertsPopup();
            }
            catch (OperationCanceledException)
            {
                // Carga cancelada. No hacer nada. Limpiar la UI.
                CurrentPatient = null;
                ActiveAlerts.Clear();
                // ... (otras limpiezas si es necesario)
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

        // --- Gestión de Datos del Paciente y FUSIÓN ---
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
                    var patientRepo = scope.ServiceProvider.GetRequiredService<IPatientRepository>();
                    var linkedDocRepo = scope.ServiceProvider.GetRequiredService<IRepository<LinkedDocument>>();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    bool docChanged = !string.Equals(_originalPatientState.DocumentNumber, CurrentPatient.DocumentNumber, StringComparison.OrdinalIgnoreCase) ||
                                      _originalPatientState.DocumentType != CurrentPatient.DocumentType;

                    if (docChanged)
                    {
                        var duplicate = await context.Patients.AsNoTracking()
                                                .FirstOrDefaultAsync(p => p.Id != CurrentPatient.Id &&
                                                                     p.DocumentNumber.ToLower() == CurrentPatient.DocumentNumber.ToLower());

                        if (duplicate == null)
                        {
                            var linkedMatch = await context.LinkedDocuments.AsNoTracking()
                                .Include(d => d.Patient)
                                .FirstOrDefaultAsync(d => d.PatientId != CurrentPatient.Id && d.DocumentNumber.ToLower() == CurrentPatient.DocumentNumber.ToLower());

                            if (linkedMatch != null) duplicate = linkedMatch.Patient;
                        }

                        if (duplicate != null)
                        {
                            var mergeResult = _dialogService.ShowConfirmation(
                                $"EL DOCUMENTO YA EXISTE.\nEl documento '{CurrentPatient.DocumentNumber}' pertenece a: {duplicate.PatientDisplayInfo}.\n¿Desea FUSIONAR este paciente con el existente?",
                                "Fusionar Pacientes");

                            if (mergeResult == CoreDialogResult.Yes)
                            {
                                var tempNewDocNumber = CurrentPatient.DocumentNumber;
                                var tempNewDocType = CurrentPatient.DocumentType;
                                CurrentPatient.DocumentNumber = _originalPatientState.DocumentNumber;
                                CurrentPatient.DocumentType = _originalPatientState.DocumentType;

                                bool success = await MergePatientHistoryAsync(CurrentPatient, duplicate);
                                if (success)
                                {
                                    _dialogService.ShowMessage("Fusión completada con éxito. Se le redirigirá al inicio.", "Listo");
                                    WeakReferenceMessenger.Default.Send(new NavigateToNewBudgetMessage(null!));
                                    return;
                                }
                                else
                                {
                                    CurrentPatient.DocumentNumber = tempNewDocNumber;
                                    CurrentPatient.DocumentType = tempNewDocType;
                                }
                            }
                            CurrentPatient.CopyFrom(_originalPatientState);
                            return;
                        }
                        if (!string.IsNullOrWhiteSpace(_originalPatientState.DocumentNumber))
                        {
                            var oldDoc = new LinkedDocument
                            {
                                PatientId = CurrentPatient.Id,
                                DocumentType = _originalPatientState.DocumentType,
                                DocumentNumber = _originalPatientState.DocumentNumber,
                                Notes = $"Documento anterior (Archivado el {DateTime.Now:dd/MM/yy})"
                            };
                            context.LinkedDocuments.Add(oldDoc);
                            Application.Current.Dispatcher.Invoke(() => LinkedDocuments.Add(oldDoc));
                        }
                    }

                    var patientToUpdate = await context.Patients.FindAsync(CurrentPatient.Id);
                    if (patientToUpdate != null)
                    {
                        context.Entry(patientToUpdate).CurrentValues.SetValues(CurrentPatient);
                        await context.SaveChangesAsync();
                        string msg = docChanged ? "Datos actualizados. Documento antiguo archivado." : "Datos actualizados.";
                        _dialogService.ShowMessage(msg, "Éxito");
                        _originalPatientState = CurrentPatient.DeepCopy();
                        IsPatientDataReadOnly = true;
                    }
                }
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error al guardar: {ex.Message}", "Error"); }
            finally { SavePatientDataAsyncCommand.NotifyCanExecuteChanged(); }
        }

        private async Task<bool> MergePatientHistoryAsync(Patient source, Patient target)
        {
            if (source.Id == target.Id) return false;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    using var transaction = await context.Database.BeginTransactionAsync();
                    try
                    {
                        await context.Database.ExecuteSqlRawAsync("UPDATE Budgets SET PatientId = {0} WHERE PatientId = {1}", target.Id, source.Id);
                        await context.Database.ExecuteSqlRawAsync("UPDATE ClinicalEntries SET PatientId = {0} WHERE PatientId = {1}", target.Id, source.Id);
                        await context.Database.ExecuteSqlRawAsync("UPDATE Payments SET PatientId = {0} WHERE PatientId = {1}", target.Id, source.Id);
                        await context.Database.ExecuteSqlRawAsync("UPDATE Prescriptions SET PatientId = {0} WHERE PatientId = {1}", target.Id, source.Id);
                        await context.Database.ExecuteSqlRawAsync("UPDATE TreatmentPlanItems SET PatientId = {0} WHERE PatientId = {1}", target.Id, source.Id);
                        await context.Database.ExecuteSqlRawAsync("UPDATE LinkedDocuments SET PatientId = {0} WHERE PatientId = {1}", target.Id, source.Id);

                        var sourceDocHistory = new LinkedDocument
                        {
                            PatientId = target.Id,
                            DocumentType = source.DocumentType,
                            DocumentNumber = source.DocumentNumber,
                            Notes = $"Fusión: {source.Name} {source.Surname} ({source.DocumentNumber}) - Original ID {source.Id}"
                        };
                        context.LinkedDocuments.Add(sourceDocHistory);

                        var sourceEntity = await context.Patients.FindAsync(source.Id);
                        if (sourceEntity != null)
                        {
                            sourceEntity.IsActive = false;
                            sourceEntity.Notes = (sourceEntity.Notes ?? "") + $"\n[FUSIONADO a Paciente: {target.Name} {target.Surname} (Doc: {target.DocumentNumber}) - ID {target.Id} el {DateTime.Now}]";
                        }
                        await context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        return true;
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error CRÍTICO en la fusión: {ex.Message}", "Error");
                return false;
            }
        }

        // --- Gestión de Documentos Vinculados ---
        private async Task AddLinkedDocumentAsync()
        {
            if (CurrentPatient == null) return;
            if (!CanManageDocuments)
            {
                _dialogService.ShowMessage("No tiene permisos para gestionar documentos vinculados.", "Acceso Denegado");
                return;
            }
            var (ok, docType, docNum, notes) = _dialogService.ShowLinkedDocumentDialog();
            if (!ok || string.IsNullOrWhiteSpace(docNum)) return;
            if (!_validationService.IsValidDocument(docNum, docType))
            {
                _dialogService.ShowMessage($"Documento inválido.", "Error");
                return;
            }
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var patientRepo = scope.ServiceProvider.GetRequiredService<IPatientRepository>();
                    var linkedDocRepo = scope.ServiceProvider.GetRequiredService<IRepository<LinkedDocument>>();
                    var duplicate = (await patientRepo.SearchByNameOrDniAsync(docNum, true, 1, 10)).FirstOrDefault();

                    if (duplicate != null)
                    {
                        if (duplicate.Id == CurrentPatient.Id)
                        {
                            _dialogService.ShowMessage($"Documento ya asignado.", "Info");
                            await LoadPatient(CurrentPatient);
                            return;
                        }
                        if (!duplicate.IsActive)
                        {
                            if (_dialogService.ShowConfirmation($"Documento en paciente archivado. ¿Recuperar?", "Recuperar") != CoreDialogResult.Yes) return;
                        }
                        else
                        {
                            _dialogService.ShowMessage($"Documento duplicado en paciente activo.", "Error");
                            return;
                        }
                    }
                    var newDoc = new LinkedDocument { PatientId = CurrentPatient.Id, DocumentType = docType, DocumentNumber = docNum, Notes = notes };
                    await linkedDocRepo.AddAsync(newDoc);
                    await linkedDocRepo.SaveChangesAsync();
                    LinkedDocuments.Add(newDoc);
                }
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); }
        }

        private async Task DeleteLinkedDocumentAsync()
        {
            if (SelectedLinkedDocument == null || CurrentPatient == null) return;
            if (_dialogService.ShowConfirmation($"¿Eliminar documento '{SelectedLinkedDocument.DocumentNumber}'?", "Confirmar") == CoreDialogResult.No) return;
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
            catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); }
        }

        // --- Alertas Médicas ---
        private async Task LoadAlertsAsync(IPatientAlertRepository repo, int patientId, CancellationToken token)
        {
            ActiveAlerts.Clear();
            var alerts = await repo.GetActiveAlertsForPatientAsync(patientId, token);
            token.ThrowIfCancellationRequested();
            foreach (var alert in alerts)
            {
                ActiveAlerts.Add(alert);
            }
            OnPropertyChanged(nameof(HasActiveAlerts)); // Notificar cambio
        }

        private void ShowCriticalAlertsPopup()
        {
            if (ActiveAlerts.Any(a => a.Level == AlertLevel.Critical))
            {
                var criticalMessages = ActiveAlerts
                    .Where(a => a.Level == AlertLevel.Critical)
                    .Select(a => $"- {a.Message.ToUpper()}");

                var message = $"¡¡ALERTA CRÍTICA!!\n\nEl paciente tiene las siguientes condiciones vitales:\n\n{string.Join("\n", criticalMessages)}";

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _dialogService.ShowMessage(message, "¡¡ALERTA CRÍTICA!!");
                });
            }
        }

        private bool CanAddAlert() => !string.IsNullOrWhiteSpace(NewAlertMessage);

        partial void OnNewAlertMessageChanged(string value)
        {
            AddAlertCommand.NotifyCanExecuteChanged();
        }

        private async Task AddAlertAsync()
        {
            if (CurrentPatient == null || !CanAddAlert()) return;

            var newAlert = new PatientAlert
            {
                PatientId = CurrentPatient.Id,
                Message = NewAlertMessage.Trim(),
                Level = NewAlertLevel,
                IsActive = true
            };

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IPatientAlertRepository>();
                    await repo.AddAsync(newAlert);
                    await repo.SaveChangesAsync();
                }

                ActiveAlerts.Add(newAlert);
                var sortedAlerts = ActiveAlerts.OrderByDescending(a => a.Level).ToList();
                ActiveAlerts.Clear();
                sortedAlerts.ForEach(ActiveAlerts.Add);

                NewAlertMessage = string.Empty;
                NewAlertLevel = AlertLevel.Warning;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al guardar la alerta: {ex.Message}", "Error BD");
            }
        }

        private async Task DeleteAlertAsync(PatientAlert? alert)
        {
            if (alert == null) return;
            if (_dialogService.ShowConfirmation($"¿Eliminar alerta '{alert.Message}'?", "Confirmar") == CoreDialogResult.No) return;

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IPatientAlertRepository>();
                    var dbAlert = await repo.GetByIdAsync(alert.Id);
                    if (dbAlert != null)
                    {
                        repo.Remove(dbAlert);
                        await repo.SaveChangesAsync();
                    }
                }
                ActiveAlerts.Remove(alert);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al eliminar la alerta: {ex.Message}", "Error BD");
            }
        }

        // --- Carga con Token ---
        private async Task LoadPendingTasksAsync(ITreatmentPlanItemRepository planItemRepo, int patientId, CancellationToken token)
        {
            PendingTasks.Clear();
            var tasks = await planItemRepo.GetTasksForPatientAsync(patientId);
            token.ThrowIfCancellationRequested();
            foreach (var task in tasks.OrderBy(t => t.IsDone).ThenByDescending(t => t.DateAdded))
            {
                token.ThrowIfCancellationRequested();
                PendingTasks.Add(task);
            }
            PendingTaskCount = PendingTasks.Count(t => !t.IsDone);
        }

        private async Task LoadAvailableTreatments(ITreatmentRepository repo, CancellationToken token)
        {
            AvailableTreatments.Clear();
            var list = await repo.GetAllAsync();
            token.ThrowIfCancellationRequested();
            foreach (var t in list.Where(x => x.IsActive).OrderBy(x => x.Name))
            {
                token.ThrowIfCancellationRequested();
                AvailableTreatments.Add(t);
            }
        }

        // --- Notificaciones de Propiedades ---
        partial void OnCurrentPatientChanged(Patient? oldValue, Patient? newValue)
        {
            if (oldValue != null) { oldValue.PropertyChanged -= CurrentPatient_PropertyChanged; oldValue.ErrorsChanged -= CurrentPatient_ErrorsChanged; }
            if (newValue != null) { newValue.PropertyChanged += CurrentPatient_PropertyChanged; newValue.ErrorsChanged += CurrentPatient_ErrorsChanged; _originalPatientState = newValue.DeepCopy(); }
            else { _originalPatientState = null; }
            SavePatientDataAsyncCommand.NotifyCanExecuteChanged();
        }

        private void CurrentPatient_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        { if (!IsPatientDataReadOnly) SavePatientDataAsyncCommand.NotifyCanExecuteChanged(); }

        private void CurrentPatient_ErrorsChanged(object? sender, System.ComponentModel.DataErrorsChangedEventArgs e)
        { if (!IsPatientDataReadOnly) SavePatientDataAsyncCommand.NotifyCanExecuteChanged(); }

        private void ToggleEditPatientData()
        {
            IsPatientDataReadOnly = !IsPatientDataReadOnly;
            if (!IsPatientDataReadOnly && CurrentPatient != null) { _originalPatientState = CurrentPatient.DeepCopy(); CurrentPatient.ForceValidation(); }
            else if (CurrentPatient != null && _originalPatientState != null) { CurrentPatient.CopyFrom(_originalPatientState); _originalPatientState = null; }
            // SavePatientDataAsyncCommand.NotifyCanExecuteChanged() se llama desde OnIsPatientDataReadOnlyChanged
        }

        private bool CanSavePatientData() => !IsPatientDataReadOnly && HasPatientDataChanged() && CurrentPatient != null && !CurrentPatient.HasErrors;
        private bool HasPatientDataChanged()
        {
            if (_originalPatientState == null || CurrentPatient == null) return false;
            return _originalPatientState.Name != CurrentPatient.Name || _originalPatientState.Surname != CurrentPatient.Surname ||
                   _originalPatientState.DocumentType != CurrentPatient.DocumentType || _originalPatientState.DocumentNumber != CurrentPatient.DocumentNumber ||
                   _originalPatientState.DateOfBirth != CurrentPatient.DateOfBirth || _originalPatientState.Phone != CurrentPatient.Phone ||
                   _originalPatientState.Address != CurrentPatient.Address || _originalPatientState.Email != CurrentPatient.Email ||
                   _originalPatientState.Notes != CurrentPatient.Notes;
        }

        // --- Métodos de Facturación (Optimizados y con Token) ---
        private async Task AllocatePayment()
        {
            if (SelectedCharge == null || SelectedPayment == null || AmountToAllocate <= 0) return;
            if (AmountToAllocate > SelectedPayment.UnallocatedAmount || AmountToAllocate > SelectedCharge.Balance)
            {
                _dialogService.ShowMessage("Cantidad inválida.", "Error");
                return;
            }
            var alloc = new PaymentAllocation { PaymentId = SelectedPayment.Id, ClinicalEntryId = SelectedCharge.Id, AmountAllocated = AmountToAllocate };
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IRepository<PaymentAllocation>>();
                    await repo.AddAsync(alloc);
                    await repo.SaveChangesAsync();
                }
                await RefreshBillingCollections();
                SelectedCharge = null; SelectedPayment = null; AmountToAllocate = 0;
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); }
        }
        private bool CanAllocate() => SelectedCharge != null && SelectedPayment != null && AmountToAllocate > 0;

        private void AutoFillAmountToAllocate()
        {
            if (SelectedCharge != null && SelectedPayment != null) AmountToAllocate = Math.Min(SelectedCharge.Balance, SelectedPayment.UnallocatedAmount);
            else AmountToAllocate = 0;
        }

        private async Task RefreshBillingCollections(CancellationToken token = default)
        {
            if (CurrentPatient == null) return;

            decimal charged = 0;
            decimal paid = 0;
            IEnumerable<ClinicalEntry> clinicalHistory = Enumerable.Empty<ClinicalEntry>();
            IEnumerable<Payment> paymentHistory = Enumerable.Empty<Payment>();

            using (var scope = _scopeFactory.CreateScope())
            {
                var cRepo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();
                var pRepo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

                var historyTask = cRepo.GetHistoryForPatientAsync(CurrentPatient.Id);
                var paymentsTask = pRepo.GetPaymentsForPatientAsync(CurrentPatient.Id);
                var totalChargedTask = cRepo.GetTotalChargedForPatientAsync(CurrentPatient.Id);
                var totalPaidTask = pRepo.GetTotalPaidForPatientAsync(CurrentPatient.Id);

                await Task.WhenAll(historyTask, paymentsTask, totalChargedTask, totalPaidTask);
                token.ThrowIfCancellationRequested();

                clinicalHistory = await historyTask;
                paymentHistory = await paymentsTask;
                charged = await totalChargedTask;
                paid = await totalPaidTask;
            }

            token.ThrowIfCancellationRequested();

            VisitHistory.Clear();
            PaymentHistory.Clear();
            clinicalHistory.ToList().ForEach(VisitHistory.Add);
            paymentHistory.ToList().ForEach(PaymentHistory.Add);

            TotalCharged = charged;
            TotalPaid = paid;
            CurrentBalance = TotalCharged - TotalPaid;

            PendingCharges.Clear();
            VisitHistory.Where(c => c.Balance > 0).OrderBy(c => c.VisitDate).ToList().ForEach(PendingCharges.Add);

            UnallocatedPayments.Clear();
            PaymentHistory.Where(p => p.UnallocatedAmount > 0).OrderBy(p => p.PaymentDate).ToList().ForEach(UnallocatedPayments.Add);

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
                        await repo.AddAsync(entry);
                        await repo.SaveChangesAsync();
                        await RefreshBillingCollections();
                        _dialogService.ShowMessage("Cargo registrado.", "Éxito");
                    }
                    catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); }
                }
            }
        }

        private async Task RegisterNewPayment()
        {
            if (CurrentPatient == null) return;
            var (ok, amount, method, obs, date) = _dialogService.ShowNewPaymentDialog();
            if (!ok || amount <= 0) return;
            var pay = new Payment { PatientId = CurrentPatient.Id, PaymentDate = date ?? DateTime.Now, Amount = amount, Method = method, Observaciones = obs };
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
                    await repo.AddAsync(pay);
                    await repo.SaveChangesAsync();
                }
                await RefreshBillingCollections();
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); }
        }

        private bool CanDeleteClinicalEntry(ClinicalEntry? entry) => entry != null;
        private async Task DeleteClinicalEntryAsync(ClinicalEntry? entry)
        {
            if (entry == null || CurrentPatient == null) return;
            if (_dialogService.ShowConfirmation("¿Eliminar cargo?", "Confirmar") == CoreDialogResult.No) return;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();
                    await repo.DeleteEntryAndAllocationsAsync(entry.Id);
                }
                await RefreshBillingCollections();
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); }
        }

        // --- Navegación y PDF ---
        private void NewBudget() { if (CurrentPatient != null) WeakReferenceMessenger.Default.Send(new NavigateToNewBudgetMessage(CurrentPatient)); }

        private async Task PrintHistoryAsync()
        {
            if (CurrentPatient == null) return;
            _dialogService.ShowMessage("Generando PDF...", "Espere");
            try
            {
                byte[] pdfBytes;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var svc = scope.ServiceProvider.GetRequiredService<IPdfService>();
                    pdfBytes = await svc.GenerateHistoryPdfAsync(CurrentPatient, VisitHistory.ToList(), PaymentHistory.ToList(), CurrentBalance);
                }
                string path = Path.Combine(Path.GetTempPath(), $"Historial_{CurrentPatient.Surname}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
                await File.WriteAllBytesAsync(path, pdfBytes);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); }
        }

        private async Task PrintOdontogramAsync()
        {
            if (CurrentPatient == null) return;
            try
            {
                string json = JsonSerializer.Serialize(Odontogram);
                string path;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var svc = scope.ServiceProvider.GetRequiredService<IPdfService>();
                    path = await svc.GenerateOdontogramPdfAsync(CurrentPatient, json);
                }
                if (_dialogService.ShowConfirmation($"PDF generado: {path}\n¿Abrir?", "Éxito") == CoreDialogResult.Yes)
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); }
        }

        // --- Odontograma ---
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
            foreach (var t in Odontogram)
            {
                t.FullCondition = ToothCondition.Sano; t.OclusalCondition = ToothCondition.Sano; t.MesialCondition = ToothCondition.Sano;
                t.DistalCondition = ToothCondition.Sano; t.VestibularCondition = ToothCondition.Sano; t.LingualCondition = ToothCondition.Sano;
                t.FullRestoration = ToothRestoration.Ninguna; t.OclusalRestoration = ToothRestoration.Ninguna; t.MesialRestoration = ToothRestoration.Ninguna;
                t.DistalRestoration = ToothRestoration.Ninguna; t.VestibularRestoration = ToothRestoration.Ninguna; t.LingualRestoration = ToothRestoration.Ninguna;
            }
            if (CurrentPatient == null || string.IsNullOrWhiteSpace(CurrentPatient.OdontogramStateJson)) return;
            try
            {
                var saved = JsonSerializer.Deserialize<List<ToothViewModel>>(CurrentPatient.OdontogramStateJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (saved == null) return;
                foreach (var s in saved)
                {
                    var m = Odontogram.FirstOrDefault(t => t.ToothNumber == s.ToothNumber);
                    if (m != null)
                    {
                        m.FullCondition = s.FullCondition; m.OclusalCondition = s.OclusalCondition; m.MesialCondition = s.MesialCondition;
                        m.DistalCondition = s.DistalCondition; m.VestibularCondition = s.VestibularCondition; m.LingualCondition = s.LingualCondition;
                        m.FullRestoration = s.FullRestoration; m.OclusalRestoration = s.OclusalRestoration; m.MesialRestoration = s.MesialRestoration;
                        m.DistalRestoration = s.DistalRestoration; m.VestibularRestoration = s.VestibularRestoration; m.LingualRestoration = s.LingualRestoration;
                    }
                }
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error JSON: {ex.Message}", "Error"); }
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
                    vm.LoadState(Odontogram, CurrentPatient);
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
                        }
                    }
                }
                LoadOdontogramStateFromJson();
                OdontogramPreviewVM.LoadFromMaster(Odontogram);
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

        // --- MÉTODOS DE PLAN DE TRATAMIENTO (QUE FALTABAN) ---
        private bool CanAddPlanItem() => !string.IsNullOrWhiteSpace(NewPlanItemDescription);

        private async Task AddPlanItemAsync()
        {
            if (CurrentPatient == null || !CanAddPlanItem()) return;
            var newItem = new TreatmentPlanItem { PatientId = CurrentPatient.Id, Description = NewPlanItemDescription, IsDone = false, DateAdded = DateTime.Now };
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var planItemRepo = scope.ServiceProvider.GetRequiredService<ITreatmentPlanItemRepository>();
                    await planItemRepo.AddAsync(newItem);
                    await planItemRepo.SaveChangesAsync();
                    await LoadPendingTasksAsync(planItemRepo, CurrentPatient.Id, CancellationToken.None); // Usar CancellationToken.None
                }
                NewPlanItemDescription = string.Empty;
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error BD"); }
        }

        private async Task TogglePlanItemAsync(TreatmentPlanItem? item)
        {
            if (item == null || CurrentPatient == null) return;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<ITreatmentPlanItemRepository>();
                    var dbItem = await repo.GetByIdAsync(item.Id);
                    if (dbItem != null)
                    {
                        dbItem.IsDone = !dbItem.IsDone;
                        repo.Update(dbItem);
                        await repo.SaveChangesAsync();
                        await LoadPendingTasksAsync(repo, CurrentPatient.Id, CancellationToken.None); // Usar CancellationToken.None
                    }
                }
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); }
        }

        private async Task DeletePlanItemAsync(TreatmentPlanItem? item)
        {
            if (item == null || CurrentPatient == null) return;
            if (_dialogService.ShowConfirmation("¿Eliminar tarea?", "Confirmar") == CoreDialogResult.No) return;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<ITreatmentPlanItemRepository>();
                    var dbItem = await repo.GetByIdAsync(item.Id);
                    if (dbItem != null)
                    {
                        repo.Remove(dbItem);
                        await repo.SaveChangesAsync();
                        await LoadPendingTasksAsync(repo, CurrentPatient.Id, CancellationToken.None); // Usar CancellationToken.None
                    }
                }
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); }
        }

        private async Task CheckPendingTasksAsync()
        {
            await Task.Delay(50);
            if (PendingTaskCount > 0)
            {
                var descs = string.Join("\n- ", PendingTasks.Where(t => !t.IsDone).Select(t => t.Description));
                _dialogService.ShowMessage($"Tareas pendientes:\n\n- {descs}", "Aviso");
            }
        }
    }
}