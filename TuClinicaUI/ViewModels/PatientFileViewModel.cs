using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

namespace TuClinica.UI.ViewModels
{
    public partial class PatientFileViewModel : BaseViewModel, IRecipient<RegisterTreatmentMessage>
    {
        // --- Servicios ---
        private readonly IClinicalEntryRepository _clinicalEntryRepo;
        private readonly IPaymentRepository _paymentRepo;
        private readonly IRepository<PaymentAllocation> _allocationRepo;
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITreatmentRepository _treatmentRepository;
        private readonly IFileDialogService _fileDialogService;
        private readonly IPdfService _pdfService;

        // --- Estado Maestro ---
        [ObservableProperty]
        private Patient? _currentPatient;
        public ObservableCollection<ToothViewModel> Odontogram { get; } = new();

        // --- Colecciones de Historial ---
        [ObservableProperty]
        private ObservableCollection<ClinicalEntry> _visitHistory = new();
        [ObservableProperty]
        private ObservableCollection<Payment> _paymentHistory = new();

        // --- Resumen de Saldo ---
        [ObservableProperty]
        private decimal _totalCharged;
        [ObservableProperty]
        private decimal _totalPaid;
        [ObservableProperty]
        private decimal _currentBalance;

        // --- Colecciones Facturación (Asignación de Pagos) ---
        [ObservableProperty]
        private ObservableCollection<ClinicalEntry> _pendingCharges = new();
        [ObservableProperty]
        private ObservableCollection<Payment> _unallocatedPayments = new();

        // --- Estado de Asignación (Panel de Facturación) ---
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AllocatePaymentCommand))]
        private ClinicalEntry? _selectedCharge;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AllocatePaymentCommand))]
        private Payment? _selectedPayment;
        [ObservableProperty]
        private decimal _amountToAllocate;

        // Historial de la tabla seleccionada
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeleteClinicalEntryAsyncCommand))]
        private ClinicalEntry? _selectedHistoryEntry;

        public ObservableCollection<Treatment> AvailableTreatments { get; } = new();
        [ObservableProperty]
        private Treatment? _selectedManualTreatment;
        [ObservableProperty]
        private string _manualChargeConcept = string.Empty;
        [ObservableProperty]
        private decimal _manualChargePrice;

        // --- DECLARACIÓN MANUAL DE COMANDOS CRÍTICOS ---
        public IAsyncRelayCommand RegisterManualChargeAsyncCommand { get; }
        public IAsyncRelayCommand DeleteClinicalEntryAsyncCommand { get; }
        public IRelayCommand PreviousPageCommand { get; }
        public IRelayCommand NextPageCommand { get; }
        public IRelayCommand ToggleEditPatientDataCommand { get; }
        public IAsyncRelayCommand SavePatientDataAsyncCommand { get; }
        public IAsyncRelayCommand AllocatePaymentCommand { get; }
        public IAsyncRelayCommand RegisterNewPaymentCommand { get; }

        // --- [NUEVOS COMANDOS] ---
        public IAsyncRelayCommand PrintOdontogramCommand { get; }
        public IRelayCommand NewBudgetCommand { get; }
        public IAsyncRelayCommand PrintHistoryCommand { get; }


        [ObservableProperty]
        private bool _hasNextPage;
        [ObservableProperty]
        private bool _isPatientDataReadOnly = true;
        [ObservableProperty]
        private string _searchText = string.Empty;
        [ObservableProperty]
        private int _currentPage = 1;
        [ObservableProperty]
        private int _pageSize = 10;
        [ObservableProperty]
        private int _totalPages = 1;
        [ObservableProperty]
        private ObservableCollection<ClinicalEntry> _filteredVisitHistory = new();
        [ObservableProperty]
        private ObservableCollection<Payment> _filteredPaymentHistory = new();
        [ObservableProperty]
        private OdontogramPreviewViewModel _odontogramPreviewVM = new();
        [ObservableProperty]
        private bool _isLoading = false;

        public PatientFileViewModel(
          IClinicalEntryRepository clinicalEntryRepo,
          IPaymentRepository paymentRepo,
          IRepository<PaymentAllocation> allocationRepo,
          IAuthService authService,
          IDialogService dialogService,
          IServiceProvider serviceProvider,
          ITreatmentRepository treatmentRepository,
          IFileDialogService fileDialogService,
          IPdfService pdfService)
        {
            _clinicalEntryRepo = clinicalEntryRepo;
            _paymentRepo = paymentRepo;
            _allocationRepo = allocationRepo;
            _authService = authService;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            _treatmentRepository = treatmentRepository;
            _fileDialogService = fileDialogService;
            _pdfService = pdfService;

            InitializeOdontogram();
            WeakReferenceMessenger.Default.Register<OpenOdontogramMessage>(this, (r, m) => OpenOdontogramWindow());
            WeakReferenceMessenger.Default.Register<RegisterTreatmentMessage>(this);

            // --- INICIALIZACIÓN MANUAL DE COMANDOS ---
            DeleteClinicalEntryAsyncCommand = new AsyncRelayCommand(DeleteClinicalEntryAsync, CanDeleteClinicalEntry);
            RegisterManualChargeAsyncCommand = new AsyncRelayCommand(RegisterManualChargeAsync);
            PreviousPageCommand = new RelayCommand(PreviousPage, () => CurrentPage > 1);
            NextPageCommand = new RelayCommand(NextPage, () => CurrentPage < TotalPages);
            ToggleEditPatientDataCommand = new RelayCommand(ToggleEditPatientData);
            SavePatientDataAsyncCommand = new AsyncRelayCommand(SavePatientDataAsync);
            AllocatePaymentCommand = new AsyncRelayCommand(AllocatePayment, CanAllocate);
            RegisterNewPaymentCommand = new AsyncRelayCommand(RegisterNewPayment);

            // --- [NUEVOS COMANDOS] ---
            PrintOdontogramCommand = new AsyncRelayCommand(PrintOdontogramAsync);
            NewBudgetCommand = new RelayCommand(NewBudget);
            PrintHistoryCommand = new AsyncRelayCommand(PrintHistoryAsync);
            // --- FIN INICIALIZACIÓN MANUAL ---

            _unallocatedPayments.CollectionChanged += (s, e) => AllocatePaymentCommand.NotifyCanExecuteChanged();
            _pendingCharges.CollectionChanged += (s, e) => AllocatePaymentCommand.NotifyCanExecuteChanged();
        }

        public async Task LoadPatient(Patient patient)
        {
            if (_isLoading) return;
            try
            {
                _isLoading = true;
                CurrentPatient = patient;
                IsPatientDataReadOnly = true; // Asegurarse de que esté en modo lectura al cargar
                var clinicalHistoryTask = _clinicalEntryRepo.GetHistoryForPatientAsync(patient.Id);
                var paymentHistoryTask = _paymentRepo.GetPaymentsForPatientAsync(patient.Id);
                var treatmentsTask = LoadAvailableTreatments();
                await Task.WhenAll(clinicalHistoryTask, paymentHistoryTask, treatmentsTask);

                var clinicalHistory = (await clinicalHistoryTask).ToList();
                var paymentHistory = (await paymentHistoryTask).ToList();

                VisitHistory.Clear();
                PaymentHistory.Clear();
                clinicalHistory.ForEach(VisitHistory.Add);
                paymentHistory.ForEach(PaymentHistory.Add);

                TotalCharged = VisitHistory.Sum(c => c.TotalCost);
                TotalPaid = PaymentHistory.Sum(p => p.Amount);
                CurrentBalance = TotalCharged - TotalPaid;

                PendingCharges.Clear();
                VisitHistory.Where(c => c.Balance > 0).OrderBy(c => c.VisitDate).ToList().ForEach(PendingCharges.Add);
                UnallocatedPayments.Clear();
                PaymentHistory.Where(p => p.UnallocatedAmount > 0).OrderBy(p => p.PaymentDate).ToList().ForEach(UnallocatedPayments.Add);

                CalculateFinalOdontogramState();
                OdontogramPreviewVM.LoadFromMaster(this.Odontogram);

                CurrentPage = 1;
                ApplyFiltersAndPaging();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al cargar ficha: {ex.Message}", "Error");
            }
            finally
            {
                _isLoading = false;
            }
        }

        // ***** INICIO DE LA CORRECCIÓN CS0229 *****
        // Se ha eliminado el atributo [RelayCommand] de este método.
        private async Task AllocatePayment()
        // ***** FIN DE LA CORRECCIÓN CS0229 *****
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
                await _allocationRepo.AddAsync(allocation);
                await _allocationRepo.SaveChangesAsync();

                // Recarga ligera en lugar de LoadPatient completo para evitar parpadeo
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

        // --- [NUEVO] Método para recarga ligera de datos financieros ---
        private async Task RefreshBillingCollections()
        {
            if (CurrentPatient == null) return;

            var clinicalHistoryTask = _clinicalEntryRepo.GetHistoryForPatientAsync(CurrentPatient.Id);
            var paymentHistoryTask = _paymentRepo.GetPaymentsForPatientAsync(CurrentPatient.Id);
            await Task.WhenAll(clinicalHistoryTask, paymentHistoryTask);

            var clinicalHistory = (await clinicalHistoryTask).ToList();
            var paymentHistory = (await paymentHistoryTask).ToList();

            // 1. Actualizar colecciones maestras
            VisitHistory.Clear();
            PaymentHistory.Clear();
            clinicalHistory.ForEach(VisitHistory.Add);
            paymentHistory.ForEach(PaymentHistory.Add);

            // 2. Recalcular saldos
            TotalCharged = VisitHistory.Sum(c => c.TotalCost);
            TotalPaid = PaymentHistory.Sum(p => p.Amount);
            CurrentBalance = TotalCharged - TotalPaid;

            // 3. Recalcular colecciones de asignación
            PendingCharges.Clear();
            VisitHistory.Where(c => c.Balance > 0).OrderBy(c => c.VisitDate).ToList().ForEach(PendingCharges.Add);
            UnallocatedPayments.Clear();
            PaymentHistory.Where(p => p.UnallocatedAmount > 0).OrderBy(p => p.PaymentDate).ToList().ForEach(UnallocatedPayments.Add);

            // 4. Refrescar la vista de historial paginada
            ApplyFiltersAndPaging();
        }

        // --- IMPLEMENTACIÓN DE LOS NUEVOS COMANDOS DE QUICK ACTION ---
        private void NewBudget()
        {
            if (CurrentPatient == null) return;
            _dialogService.ShowMessage(
               $"Funcionalidad Pendiente: Abrir vista de Presupuestos e iniciar nuevo presupuesto para {CurrentPatient.PatientDisplayInfo}. (Esta acción debería navegar a BudgetsView).",
               "Navegación Pendiente");
        }

        private async Task PrintHistoryAsync()
        {
            if (CurrentPatient == null) return;

            _dialogService.ShowMessage(
                $"Funcionalidad Pendiente: Generar el PDF del Historial Clínico completo de {CurrentPatient.PatientDisplayInfo}.",
                "Impresión de Historial");

            await Task.CompletedTask;
        }

        private async Task PrintOdontogramAsync()
        {
            if (CurrentPatient == null) return;

            _dialogService.ShowMessage(
                $"Funcionalidad Pendiente: Generar el PDF del Odontograma actual de {CurrentPatient.PatientDisplayInfo}. (Requiere implementación en PdfService).",
                "Impresión de Odontograma");

            await Task.CompletedTask;
        }
        // --- FIN IMPLEMENTACIÓN DE NUEVOS COMANDOS DE QUICK ACTION ---

        // --- MÉTODOS EXISTENTES ---

        // ***** INICIO DE LA CORRECCIÓN CS0102/CS0229 *****
        // Se ha eliminado el atributo [RelayCommand] de este método.
        private async Task RegisterNewPayment()
        // ***** FIN DE LA CORRECCIÓN CS0102/CS0229 *****
        {
            if (CurrentPatient == null) return;

            var (ok, amount, method) = _dialogService.ShowNewPaymentDialog();
            if (!ok || amount <= 0) return;

            var newPayment = new Payment
            {
                PatientId = CurrentPatient.Id,
                PaymentDate = DateTime.Now,
                Amount = amount,
                Method = method
            };

            try
            {
                await _paymentRepo.AddAsync(newPayment);
                await _paymentRepo.SaveChangesAsync();
                await RefreshBillingCollections(); // Recarga ligera
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

        private void CalculateFinalOdontogramState()
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

            var allTreatments = VisitHistory
              .SelectMany(v => v.TreatmentsPerformed)
              .OrderBy(t => t.ClinicalEntry!.VisitDate);

            foreach (var treatment in allTreatments)
            {
                var toothVM = Odontogram.FirstOrDefault(t => t.ToothNumber == treatment.ToothNumber);
                if (toothVM == null) continue;

                ToothRestoration restoredState = MapTreatmentToRestoration(treatment.TreatmentId);
                ApplyRestorationToToothVM(toothVM, treatment.Surfaces, restoredState);

                if (restoredState != ToothRestoration.Ninguna)
                {
                    ApplyConditionToToothVM(toothVM, treatment.Surfaces, ToothCondition.Sano);
                }
            }
        }

        private ToothRestoration MapTreatmentToRestoration(int treatmentId)
        {
            // Lógica simple: si tiene un ID de tratamiento, es una obturación.
            // Esto debería mejorarse para mapear a Coronas, Implantes, etc.
            return treatmentId > 0 ? ToothRestoration.Obturacion : ToothRestoration.Ninguna;
        }

        private void ApplyRestorationToToothVM(ToothViewModel toothVM, ToothSurface surfaces, ToothRestoration restoredState)
        {
            if (surfaces.HasFlag(ToothSurface.Completo)) toothVM.FullRestoration = restoredState;
            if (surfaces.HasFlag(ToothSurface.Oclusal)) toothVM.OclusalRestoration = restoredState;
            if (surfaces.HasFlag(ToothSurface.Mesial)) toothVM.MesialRestoration = restoredState;
            if (surfaces.HasFlag(ToothSurface.Distal)) toothVM.DistalRestoration = restoredState;
            if (surfaces.HasFlag(ToothSurface.Vestibular)) toothVM.VestibularRestoration = restoredState;
            if (surfaces.HasFlag(ToothSurface.Lingual)) toothVM.LingualRestoration = restoredState;
        }

        private void ApplyConditionToToothVM(ToothViewModel toothVM, ToothSurface surfaces, ToothCondition condition)
        {
            if (surfaces.HasFlag(ToothSurface.Completo)) toothVM.FullCondition = condition;
            if (surfaces.HasFlag(ToothSurface.Oclusal)) toothVM.OclusalCondition = condition;
            if (surfaces.HasFlag(ToothSurface.Mesial)) toothVM.MesialCondition = condition;
            if (surfaces.HasFlag(ToothSurface.Distal)) toothVM.DistalCondition = condition;
            if (surfaces.HasFlag(ToothSurface.Vestibular)) toothVM.VestibularCondition = condition;
            if (surfaces.HasFlag(ToothSurface.Lingual)) toothVM.LingualCondition = condition;
        }

        partial void OnSelectedHistoryEntryChanged(ClinicalEntry? value)
        {
            DeleteClinicalEntryAsyncCommand.NotifyCanExecuteChanged();
        }

        public async void Receive(RegisterTreatmentMessage message)
        {
            await RegisterTreatmentAsync(
              message.ToothNumber,
              message.Surface,
              message.TreatmentId,
              message.RestorationResult,
              message.Price);
        }

        private async Task RegisterTreatmentAsync(int toothNum, ToothSurface surface, int treatmentId, ToothRestoration restorationResult, decimal price)
        {
            if (CurrentPatient == null || _authService.CurrentUser == null) return;

            var selectedTreatment = AvailableTreatments.FirstOrDefault(t => t.Id == treatmentId);
            if (selectedTreatment == null)
            {
                _dialogService.ShowMessage($"No se encontró el tratamiento con ID {treatmentId}.", "Error de Catálogo");
                return;
            }

            using (var scope = _serviceProvider.CreateScope())
            {
                try
                {
                    var clinicalRepo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();

                    var toothTreatment = new ToothTreatment
                    {
                        ToothNumber = toothNum,
                        Surfaces = surface,
                        TreatmentId = selectedTreatment.Id,
                        TreatmentPerformed = restorationResult, // Guardar el estado visual
                        Price = price
                    };

                    var clinicalEntry = new ClinicalEntry
                    {
                        PatientId = CurrentPatient.Id,
                        DoctorId = _authService.CurrentUser.Id,
                        VisitDate = DateTime.Now,
                        Diagnosis = $"Tratamiento: {selectedTreatment.Name} en diente {toothNum} ({surface})",
                        TotalCost = price,
                        TreatmentsPerformed = new List<ToothTreatment> { toothTreatment }
                    };

                    await clinicalRepo.AddAsync(clinicalEntry);
                    await clinicalRepo.SaveChangesAsync();
                    await RefreshBillingCollections(); // Recarga ligera
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Error al registrar el tratamiento: {ex.Message}", "Error BD");
                }
            }
        }

        [RelayCommand]
        private void OpenOdontogramWindow()
        {
            if (CurrentPatient == null)
            {
                _dialogService.ShowMessage("Debe tener un paciente cargado para abrir el odontograma.", "Error");
                return;
            }
            try
            {
                var dialog = _serviceProvider.GetRequiredService<OdontogramWindow>();
                var vm = _serviceProvider.GetRequiredService<OdontogramViewModel>();

                vm.LoadState(this.Odontogram, this.CurrentPatient);
                vm.AvailableTreatments = new ObservableCollection<Treatment>(this.AvailableTreatments);

                dialog.DataContext = vm;
                dialog.Owner = App.Current.MainWindow;
                dialog.ShowDialog();

                // [NUEVO] Al cerrar el diálogo, refrescamos el estado por si se añadieron tratamientos
                _ = RefreshBillingCollections();
                CalculateFinalOdontogramState();
                OdontogramPreviewVM.LoadFromMaster(this.Odontogram);

            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al abrir el odontograma: {ex.Message}", "Error");
            }
        }

        private bool CanDeleteClinicalEntry()
        {
            return SelectedHistoryEntry != null;
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
            if (IsPatientDataReadOnly && CurrentPatient != null)
            {
                // Deshacer cambios no guardados (recargando desde la BD)
                _ = LoadPatient(CurrentPatient);
            }
        }

        private async Task RegisterManualChargeAsync()
        {
            if (CurrentPatient == null || _authService.CurrentUser == null) return;
            if (string.IsNullOrWhiteSpace(ManualChargeConcept) || ManualChargePrice <= 0)
            {
                _dialogService.ShowMessage("Debe introducir un concepto y un precio mayor que cero.", "Datos incompletos");
                return;
            }
            using (var scope = _serviceProvider.CreateScope())
            {
                try
                {
                    var clinicalRepo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();

                    int? treatmentId = SelectedManualTreatment?.Id;

                    var clinicalEntry = new ClinicalEntry
                    {
                        PatientId = CurrentPatient.Id,
                        DoctorId = _authService.CurrentUser.Id,
                        VisitDate = DateTime.Now,
                        Diagnosis = ManualChargeConcept,
                        TotalCost = ManualChargePrice,
                    };

                    // Si se seleccionó un tratamiento, lo añadimos como un "acto"
                    if (treatmentId.HasValue)
                    {
                        clinicalEntry.TreatmentsPerformed.Add(new ToothTreatment
                        {
                            ToothNumber = 0, // 0 = "Boca Completa" o "N/A"
                            Surfaces = ToothSurface.Completo,
                            TreatmentId = treatmentId.Value,
                            TreatmentPerformed = MapTreatmentToRestoration(treatmentId.Value),
                            Price = ManualChargePrice
                        });
                    }

                    await clinicalRepo.AddAsync(clinicalEntry);
                    await clinicalRepo.SaveChangesAsync();
                    ManualChargeConcept = string.Empty;
                    ManualChargePrice = 0;
                    SelectedManualTreatment = null;
                    await RefreshBillingCollections(); // Recarga ligera
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Error al registrar el cargo manual: {ex.Message}", "Error BD");
                }
            }
        }

        private async Task LoadAvailableTreatments()
        {
            AvailableTreatments.Clear();
            var treatments = await _treatmentRepository.GetAllAsync();
            foreach (var treatment in treatments.Where(t => t.IsActive).OrderBy(t => t.Name))
            {
                AvailableTreatments.Add(treatment);
            }
        }

        private async Task SavePatientDataAsync()
        {
            if (CurrentPatient == null) return;
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var patientRepo = scope.ServiceProvider.GetRequiredService<IPatientRepository>();
                    // Usamos FindAsync con rastreo para obtener la entidad
                    var patientToUpdate = await patientRepo.GetByIdAsync(CurrentPatient.Id);
                    if (patientToUpdate != null)
                    {
                        // Transferir cambios del CurrentPatient (que está en la UI) al patientToUpdate (rastreado)
                        patientToUpdate.Phone = CurrentPatient.Phone;
                        patientToUpdate.Address = CurrentPatient.Address;
                        patientToUpdate.Email = CurrentPatient.Email;
                        patientToUpdate.Notes = CurrentPatient.Notes;

                        // No llamamos a patientRepo.Update(), EF Core ya rastrea los cambios
                        await patientRepo.SaveChangesAsync();
                        _dialogService.ShowMessage("Datos del paciente actualizados.", "Éxito");
                        IsPatientDataReadOnly = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al guardar los datos del paciente:\n{ex.Message}", "Error Base de Datos");
            }
        }

        partial void OnSelectedManualTreatmentChanged(Treatment? value)
        {
            if (value != null)
            {
                ManualChargeConcept = value.Name;
                ManualChargePrice = value.DefaultPrice;
            }
        }

        private async Task DeleteClinicalEntryAsync()
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
                bool success = await _clinicalEntryRepo.DeleteEntryAndAllocationsAsync(SelectedHistoryEntry.Id);
                if (success)
                {
                    _dialogService.ShowMessage("Cargo eliminado correctamente.", "Éxito");
                }
                else
                {
                    _dialogService.ShowMessage("No se pudo eliminar el cargo (quizás ya estaba borrado).", "Error");
                }
                await RefreshBillingCollections(); // Recarga ligera
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al eliminar el cargo: {ex.Message}", "Error de Base de Datos");
            }
        }

        // --- MÉTODOS DE PAGINACIÓN ---
        private void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
            }
        }
        private void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
            }
        }
        partial void OnSearchTextChanged(string value)
        {
            ApplyFiltersAndPaging();
        }
        partial void OnCurrentPageChanged(int value)
        {
            ApplyFiltersAndPaging();
            PreviousPageCommand.NotifyCanExecuteChanged();
            NextPageCommand.NotifyCanExecuteChanged();
        }
        partial void OnTotalPagesChanged(int value)
        {
            HasNextPage = CurrentPage < TotalPages;
            NextPageCommand.NotifyCanExecuteChanged();
        }
        private void ApplyFiltersAndPaging()
        {
            var filteredClinical = string.IsNullOrWhiteSpace(SearchText)
              ? VisitHistory.ToList()
              : VisitHistory.Where(c =>
                c.Diagnosis?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                c.DoctorName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                c.VisitDate.ToString("dd/MM/yyyy").Contains(SearchText)).ToList();

            var filteredPayments = string.IsNullOrWhiteSpace(SearchText)
              ? PaymentHistory.ToList()
              : PaymentHistory.Where(p =>
                p.Method?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                p.PaymentDate.ToString("dd/MM/yyyy").Contains(SearchText)).ToList();

            // Calcular total de páginas basado en el historial más largo
            int maxItems = Math.Max(filteredClinical.Count, filteredPayments.Count);
            TotalPages = Math.Max(1, (int)Math.Ceiling((double)maxItems / PageSize));

            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var pagedClinical = filteredClinical
              .OrderByDescending(c => c.VisitDate) // Asegurar orden
              .Skip((CurrentPage - 1) * PageSize)
              .Take(PageSize)
              .ToList();

            var pagedPayments = filteredPayments
              .OrderByDescending(p => p.PaymentDate) // Asegurar orden
              .Skip((CurrentPage - 1) * PageSize)
              .Take(PageSize)
              .ToList();

            FilteredVisitHistory.Clear();
            foreach (var item in pagedClinical) FilteredVisitHistory.Add(item);

            FilteredPaymentHistory.Clear();
            foreach (var item in pagedPayments) FilteredPaymentHistory.Add(item);
        }
    }
}