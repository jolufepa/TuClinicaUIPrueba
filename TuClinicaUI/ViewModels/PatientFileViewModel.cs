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
        [NotifyCanExecuteChangedFor(nameof(AllocatePaymentCommand))]
        private ClinicalEntry? _selectedCharge;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AllocatePaymentCommand))]
        private Payment? _selectedPayment;
        [ObservableProperty]
        private decimal _amountToAllocate;

        public ObservableCollection<Treatment> AvailableTreatments { get; } = new();

        // --- Propiedades de Plan de Tratamiento ---
        public ObservableCollection<TreatmentPlanItem> PendingTasks { get; } = new();

        [ObservableProperty]
        private int _pendingTaskCount = 0;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddPlanItemAsyncCommand))]
        private string _newPlanItemDescription = string.Empty;

        // --- Propiedades de Documentos Vinculados ---
        [ObservableProperty]
        private ObservableCollection<LinkedDocument> _linkedDocuments = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeleteLinkedDocumentCommand))]
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

        public IEnumerable<PatientDocumentType> DocumentTypes => Enum.GetValues(typeof(PatientDocumentType)).Cast<PatientDocumentType>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SavePatientDataAsyncCommand))]
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
          IValidationService validationService)
        {
            _authService = authService;
            _dialogService = dialogService;
            _scopeFactory = scopeFactory;
            _fileDialogService = fileDialogService;
            _validationService = validationService;

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

            _unallocatedPayments.CollectionChanged += (s, e) => AllocatePaymentCommand.NotifyCanExecuteChanged();
            _pendingCharges.CollectionChanged += (s, e) => AllocatePaymentCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedLinkedDocumentChanged(LinkedDocument? value)
        {
            DeleteLinkedDocumentCommand.NotifyCanExecuteChanged();
        }

        // --- Carga de Datos ---
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

                using (var scope = _scopeFactory.CreateScope())
                {
                    // 1. Cargar el paciente y sus datos vinculados (usando AppDbContext directamente para Include)
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var freshPatient = await context.Patients
                                        .Include(p => p.LinkedDocuments)
                                        .FirstOrDefaultAsync(p => p.Id == patient.Id);

                    CurrentPatient = freshPatient ?? patient;

                    // 2. Cargar el resto de los historiales
                    var planItemRepo = scope.ServiceProvider.GetRequiredService<ITreatmentPlanItemRepository>();
                    var clinicalEntryRepo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();
                    var paymentRepo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
                    var treatmentRepo = scope.ServiceProvider.GetRequiredService<ITreatmentRepository>();

                    var clinicalHistoryTask = clinicalEntryRepo.GetHistoryForPatientAsync(CurrentPatient.Id);
                    var paymentHistoryTask = paymentRepo.GetPaymentsForPatientAsync(CurrentPatient.Id);
                    var treatmentsTask = LoadAvailableTreatments(treatmentRepo);
                    var pendingTasksTask = LoadPendingTasksAsync(planItemRepo, CurrentPatient.Id);

                    await Task.WhenAll(clinicalHistoryTask, paymentHistoryTask, treatmentsTask, pendingTasksTask);

                    var clinicalHistory = (await clinicalHistoryTask).ToList();
                    var paymentHistory = (await paymentHistoryTask).ToList();

                    VisitHistory.Clear();
                    PaymentHistory.Clear();
                    clinicalHistory.ForEach(VisitHistory.Add);
                    paymentHistory.ForEach(PaymentHistory.Add);

                    // 3. Llenar la lista de documentos vinculados
                    LinkedDocuments.Clear();
                    if (CurrentPatient.LinkedDocuments != null)
                    {
                        foreach (var doc in CurrentPatient.LinkedDocuments.OrderBy(d => d.DocumentType).ThenBy(d => d.DocumentNumber))
                        {
                            LinkedDocuments.Add(doc);
                        }
                    }
                }

                IsPatientDataReadOnly = true;
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

            // Limpieza
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

                    // 1. DETECTAR SI EL DOCUMENTO HA CAMBIADO
                    bool docChanged = !string.Equals(_originalPatientState.DocumentNumber, CurrentPatient.DocumentNumber, StringComparison.OrdinalIgnoreCase) ||
                                      _originalPatientState.DocumentType != CurrentPatient.DocumentType;

                    if (docChanged)
                    {
                        // 2. BUSCAR SI EL NUEVO DOCUMENTO YA EXISTE (DUPLICADO)
                        // Buscamos primero en pacientes principales
                        var duplicate = await context.Patients.AsNoTracking()
                                                .FirstOrDefaultAsync(p => p.Id != CurrentPatient.Id &&
                                                                     p.DocumentNumber.ToLower() == CurrentPatient.DocumentNumber.ToLower());

                        // Si no está en principales, buscamos en vinculados de OTROS pacientes
                        if (duplicate == null)
                        {
                            var linkedMatch = await context.LinkedDocuments.AsNoTracking()
                                .Include(d => d.Patient)
                                .FirstOrDefaultAsync(d => d.PatientId != CurrentPatient.Id && d.DocumentNumber.ToLower() == CurrentPatient.DocumentNumber.ToLower());

                            if (linkedMatch != null) duplicate = linkedMatch.Patient;
                        }

                        if (duplicate != null)
                        {
                            // === ESCENARIO FUSIÓN ===
                            var mergeResult = _dialogService.ShowConfirmation(
                                $"EL DOCUMENTO YA EXISTE.\n\n" +
                                $"El documento '{CurrentPatient.DocumentNumber}' pertenece a: {duplicate.PatientDisplayInfo}.\n\n" +
                                $"¿Desea FUSIONAR este paciente con el existente?\n" +
                                $"(Se moverá todo el historial al paciente existente y este se archivará)",
                                "Fusionar Pacientes");

                            if (mergeResult == CoreDialogResult.Yes)
                            {
                                // === CORRECCIÓN CRÍTICA: RESTAURAR DATOS ANTIGUOS ANTES DE FUSIONAR ===
                                // Guardamos temporalmente el DNI NUEVO que ha escrito el usuario
                                var tempNewDocNumber = CurrentPatient.DocumentNumber;
                                var tempNewDocType = CurrentPatient.DocumentType;

                                // Restauramos el estado ORIGINAL (el NIE antiguo) en el objeto CurrentPatient.
                                // Esto es vital para que la fusión guarde el documento ANTIGUO en el historial.
                                CurrentPatient.DocumentNumber = _originalPatientState.DocumentNumber;
                                CurrentPatient.DocumentType = _originalPatientState.DocumentType;
                                // ---------------------------------------------------------------------

                                bool success = await MergePatientHistoryAsync(CurrentPatient, duplicate);
                                if (success)
                                {
                                    _dialogService.ShowMessage("Fusión completada con éxito. Se le redirigirá al inicio.", "Listo");
                                    // Navegar fuera para evitar errores
                                    WeakReferenceMessenger.Default.Send(new NavigateToNewBudgetMessage(null!)); // Truco para refrescar o salir
                                    return;
                                }
                                else
                                {
                                    // Si falla la fusión, restauramos los valores nuevos en pantalla
                                    CurrentPatient.DocumentNumber = tempNewDocNumber;
                                    CurrentPatient.DocumentType = tempNewDocType;
                                }
                            }

                            // Si cancela la fusión, revertimos el cambio visual al original
                            CurrentPatient.CopyFrom(_originalPatientState);
                            return;
                        }

                        // 3. SI NO ES DUPLICADO -> GUARDAR ANTIGUO EN HISTORIAL AUTOMÁTICAMENTE
                        if (!string.IsNullOrWhiteSpace(_originalPatientState.DocumentNumber))
                        {
                            var oldDoc = new LinkedDocument
                            {
                                PatientId = CurrentPatient.Id,
                                DocumentType = _originalPatientState.DocumentType,
                                DocumentNumber = _originalPatientState.DocumentNumber,
                                Notes = $"Documento anterior (Archivado el {DateTime.Now:dd/MM/yy})"
                            };

                            // Lo añadimos al contexto (se guardará con el paciente)
                            context.LinkedDocuments.Add(oldDoc);
                            // Actualizamos la UI inmediatamente
                            Application.Current.Dispatcher.Invoke(() => LinkedDocuments.Add(oldDoc));
                        }
                    }

                    // 4. GUARDAR CAMBIOS DEL PACIENTE
                    var patientToUpdate = await context.Patients.FindAsync(CurrentPatient.Id);
                    if (patientToUpdate != null)
                    {
                        context.Entry(patientToUpdate).CurrentValues.SetValues(CurrentPatient);
                        await context.SaveChangesAsync();

                        string msg = docChanged
                            ? "Datos actualizados. El documento antiguo se ha guardado en 'Documentos Vinculados'."
                            : "Datos del paciente actualizados.";

                        _dialogService.ShowMessage(msg, "Éxito");

                        _originalPatientState = CurrentPatient.DeepCopy();
                        IsPatientDataReadOnly = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al guardar: {ex.Message}", "Error");
            }
            finally
            {
                SavePatientDataAsyncCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task<bool> MergePatientHistoryAsync(Patient source, Patient target)
        {
            if (source.Id == target.Id) return false;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // Mover todos los registros relacionados
                    await context.Database.ExecuteSqlRawAsync("UPDATE Budgets SET PatientId = {0} WHERE PatientId = {1}", target.Id, source.Id);
                    await context.Database.ExecuteSqlRawAsync("UPDATE ClinicalEntries SET PatientId = {0} WHERE PatientId = {1}", target.Id, source.Id);
                    await context.Database.ExecuteSqlRawAsync("UPDATE Payments SET PatientId = {0} WHERE PatientId = {1}", target.Id, source.Id);
                    await context.Database.ExecuteSqlRawAsync("UPDATE Prescriptions SET PatientId = {0} WHERE PatientId = {1}", target.Id, source.Id);
                    await context.Database.ExecuteSqlRawAsync("UPDATE TreatmentPlanItems SET PatientId = {0} WHERE PatientId = {1}", target.Id, source.Id);
                    await context.Database.ExecuteSqlRawAsync("UPDATE LinkedDocuments SET PatientId = {0} WHERE PatientId = {1}", target.Id, source.Id);

                    // Guardar el documento principal del paciente 'source' como vinculado en 'target'
                    // AHORA SÍ: 'source' tiene el documento ANTIGUO restaurado.
                    var sourceDocHistory = new LinkedDocument
                    {
                        PatientId = target.Id,
                        DocumentType = source.DocumentType,
                        DocumentNumber = source.DocumentNumber,
                        // Guardamos una nota explicativa
                        Notes = $"Fusión: {source.Name} {source.Surname} ({source.DocumentNumber}) - Original ID {source.Id}"
                    };
                    context.LinkedDocuments.Add(sourceDocHistory);

                    // Archivar el paciente original
                    var sourceEntity = await context.Patients.FindAsync(source.Id);
                    if (sourceEntity != null)
                    {
                        sourceEntity.IsActive = false;

                        // --- CORRECCIÓN: FORMATO DETALLADO EN LA NOTA DE FUSIÓN ---
                        sourceEntity.Notes = (sourceEntity.Notes ?? "") +
                            $"\n[FUSIONADO a Paciente: {target.Name} {target.Surname} (Doc: {target.DocumentNumber}) - ID {target.Id} el {DateTime.Now}]";
                        // ----------------------------------------------------------
                    }

                    await context.SaveChangesAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error en fusión: {ex.Message}", "Error Crítico");
                return false;
            }
        }

        // --- Gestión de Documentos Vinculados (CORREGIDA LA COMPROBACIÓN MANUAL) ---
        private async Task AddLinkedDocumentAsync()
        {
            if (CurrentPatient == null) return;
            if (!CanManageDocuments)
            {
                _dialogService.ShowMessage("No tiene permisos para gestionar documentos vinculados.", "Acceso Denegado");
                return;
            }

            // 1. Mostrar el diálogo de creación
            var (ok, docType, docNum, notes) = _dialogService.ShowLinkedDocumentDialog();

            // 2. Si el usuario cancela o lo deja vacío, salir
            if (!ok || string.IsNullOrWhiteSpace(docNum))
            {
                return;
            }

            // 3. Validar el formato del documento (para DNI/NIE)
            if (!_validationService.IsValidDocument(docNum, docType))
            {
                _dialogService.ShowMessage($"El número de documento '{docNum}' no tiene un formato válido.", "Formato Inválido");
                return;
            }

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var patientRepo = scope.ServiceProvider.GetRequiredService<IPatientRepository>();
                    var linkedDocRepo = scope.ServiceProvider.GetRequiredService<IRepository<LinkedDocument>>();

                    // 4. Comprobar duplicados
                    var duplicate = (await patientRepo.SearchByNameOrDniAsync(docNum, true, 1, 10)).FirstOrDefault();

                    if (duplicate != null)
                    {
                        // --- CASO A: Si el duplicado es el MISMO paciente (actual), refrescamos y salimos
                        if (duplicate.Id == CurrentPatient.Id)
                        {
                            _dialogService.ShowMessage($"Este documento ya consta como asignado a este paciente. Actualizando lista...", "Información");
                            await LoadPatient(CurrentPatient); // RECARGAR FICHA PARA QUE APAREZCA
                            return;
                        }

                        // --- CASO B: Si el duplicado es un paciente ARCHIVADO (Inactivo), permitimos recuperar
                        if (!duplicate.IsActive)
                        {
                            var confirm = _dialogService.ShowConfirmation(
                               $"El documento '{docNum}' pertenece a un paciente archivado ({duplicate.PatientDisplayInfo}).\n\n" +
                               "¿Desea recuperar este documento y vincularlo a esta ficha activa?",
                               "Recuperar Documento");

                            if (confirm != CoreDialogResult.Yes) return;
                            // Si dice SÍ, continuamos y creamos el LinkedDocument
                        }
                        else
                        {
                            // --- CASO C: Es otro paciente ACTIVO distinto -> Bloqueo real
                            _dialogService.ShowMessage($"El documento '{docNum}' ya está asignado al paciente activo: {duplicate.PatientDisplayInfo}.", "Documento Duplicado");
                            return;
                        }
                    }

                    // 5. Crear y guardar el nuevo documento
                    var newDoc = new LinkedDocument
                    {
                        PatientId = CurrentPatient.Id,
                        DocumentType = docType,
                        DocumentNumber = docNum,
                        Notes = notes
                    };

                    await linkedDocRepo.AddAsync(newDoc);
                    await linkedDocRepo.SaveChangesAsync();

                    // 6. Añadirlo a la lista de la UI para verlo inmediatamente
                    LinkedDocuments.Add(newDoc);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al guardar el documento: {ex.Message}", "Error BD");
            }
        }

        private async Task DeleteLinkedDocumentAsync()
        {
            if (SelectedLinkedDocument == null || CurrentPatient == null) return;

            var result = _dialogService.ShowConfirmation(
                $"¿Está seguro de que desea eliminar el documento vinculado '{SelectedLinkedDocument.DocumentNumber}'?",
                "Confirmar Eliminación");

            if (result == CoreDialogResult.No) return;

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IRepository<LinkedDocument>>();
                    var docToDelete = await repo.GetByIdAsync(SelectedLinkedDocument.Id);
                    if (docToDelete != null)
                    {
                        repo.Remove(docToDelete);
                        await repo.SaveChangesAsync();
                    }
                }

                // Eliminar de la lista visual
                LinkedDocuments.Remove(SelectedLinkedDocument);
                SelectedLinkedDocument = null;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al eliminar el documento: {ex.Message}", "Error BD");
            }
        }

        // --- Otros Métodos Auxiliares (Sin Cambios) ---
        private async Task LoadPendingTasksAsync(ITreatmentPlanItemRepository planItemRepo, int patientId)
        {
            PendingTasks.Clear();
            var tasks = await planItemRepo.GetTasksForPatientAsync(patientId);
            foreach (var task in tasks.OrderBy(t => t.IsDone).ThenByDescending(t => t.DateAdded)) PendingTasks.Add(task);
            PendingTaskCount = PendingTasks.Count(t => !t.IsDone);
        }

        private bool CanAddPlanItem() => !string.IsNullOrWhiteSpace(NewPlanItemDescription);

        partial void OnNewPlanItemDescriptionChanged(string value) => AddPlanItemAsyncCommand.NotifyCanExecuteChanged();

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
                    await LoadPendingTasksAsync(planItemRepo, CurrentPatient.Id);
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
                        await LoadPendingTasksAsync(repo, CurrentPatient.Id);
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
                        await LoadPendingTasksAsync(repo, CurrentPatient.Id);
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
            SavePatientDataAsyncCommand.NotifyCanExecuteChanged();
        }

        private async Task LoadAvailableTreatments(ITreatmentRepository repo)
        {
            AvailableTreatments.Clear();
            var list = await repo.GetAllAsync();
            foreach (var t in list.Where(x => x.IsActive).OrderBy(x => x.Name)) AvailableTreatments.Add(t);
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

        // --- Métodos de Facturación ---
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

        partial void OnSelectedChargeChanged(ClinicalEntry? value) => AutoFillAmountToAllocate();
        partial void OnSelectedPaymentChanged(Payment? value) => AutoFillAmountToAllocate();
        private void AutoFillAmountToAllocate()
        {
            if (SelectedCharge != null && SelectedPayment != null) AmountToAllocate = Math.Min(SelectedCharge.Balance, SelectedPayment.UnallocatedAmount);
            else AmountToAllocate = 0;
        }

        private async Task RefreshBillingCollections()
        {
            if (CurrentPatient == null) return;
            using (var scope = _scopeFactory.CreateScope())
            {
                var cRepo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();
                var pRepo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
                var cTask = cRepo.GetHistoryForPatientAsync(CurrentPatient.Id);
                var pTask = pRepo.GetPaymentsForPatientAsync(CurrentPatient.Id);
                await Task.WhenAll(cTask, pTask);

                VisitHistory.Clear(); PaymentHistory.Clear();
                (await cTask).ToList().ForEach(VisitHistory.Add);
                (await pTask).ToList().ForEach(PaymentHistory.Add);
            }
            TotalCharged = VisitHistory.Sum(c => c.TotalCost);
            TotalPaid = PaymentHistory.Sum(p => p.Amount);
            CurrentBalance = TotalCharged - TotalPaid;

            PendingCharges.Clear(); VisitHistory.Where(c => c.Balance > 0).OrderBy(c => c.VisitDate).ToList().ForEach(PendingCharges.Add);
            UnallocatedPayments.Clear(); PaymentHistory.Where(p => p.UnallocatedAmount > 0).OrderBy(p => p.PaymentDate).ToList().ForEach(UnallocatedPayments.Add);

            HistorialCombinado.Clear();
            foreach (var c in VisitHistory) HistorialCombinado.Add(new CargoEvent(c, this));
            foreach (var p in PaymentHistory) HistorialCombinado.Add(new AbonoEvent(p));
            var sorted = HistorialCombinado.OrderByDescending(x => x.Timestamp).ToList();
            HistorialCombinado.Clear(); sorted.ForEach(HistorialCombinado.Add);
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
    }
}