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
using System.Diagnostics; // Para Process.Start

namespace TuClinica.UI.ViewModels
{
    public partial class PatientFileViewModel : BaseViewModel
    {
        // --- Servicios ---
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;
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

        // --- DECLARACIÓN MANUAL DE COMANDOS CRÍTICOS ---
        public IAsyncRelayCommand DeleteClinicalEntryAsyncCommand { get; }
        public IRelayCommand PreviousPageCommand { get; }
        public IRelayCommand NextPageCommand { get; }
        public IRelayCommand ToggleEditPatientDataCommand { get; }
        public IAsyncRelayCommand SavePatientDataAsyncCommand { get; }
        public IAsyncRelayCommand AllocatePaymentCommand { get; }
        public IAsyncRelayCommand RegisterNewPaymentCommand { get; }
        public IAsyncRelayCommand PrintOdontogramCommand { get; } 
        public IRelayCommand NewBudgetCommand { get; }
        public IAsyncRelayCommand PrintHistoryCommand { get; }
        public IAsyncRelayCommand OpenRegisterChargeDialogCommand { get; }

        // --- Comandos que SÍ se pueden generar automáticamente ---
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
          IAuthService authService,
          IDialogService dialogService,
          IServiceProvider serviceProvider,
          IFileDialogService fileDialogService,
          IPdfService pdfService)
        {
            _authService = authService;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            _fileDialogService = fileDialogService;
            _pdfService = pdfService;

            InitializeOdontogram();
            WeakReferenceMessenger.Default.Register<OpenOdontogramMessage>(this, (r, m) => OpenOdontogramWindow());

            // --- INICIALIZACIÓN MANUAL DE COMANDOS ---
            DeleteClinicalEntryAsyncCommand = new AsyncRelayCommand(DeleteClinicalEntryAsync, CanDeleteClinicalEntry);
            PreviousPageCommand = new RelayCommand(PreviousPage, () => CurrentPage > 1);
            NextPageCommand = new RelayCommand(NextPage, () => CurrentPage < TotalPages);
            ToggleEditPatientDataCommand = new RelayCommand(ToggleEditPatientData);
            SavePatientDataAsyncCommand = new AsyncRelayCommand(SavePatientDataAsync);
            AllocatePaymentCommand = new AsyncRelayCommand(AllocatePayment, CanAllocate);
            RegisterNewPaymentCommand = new AsyncRelayCommand(RegisterNewPayment);
            PrintOdontogramCommand = new AsyncRelayCommand(PrintOdontogramAsync);
            NewBudgetCommand = new RelayCommand(NewBudget);
            PrintHistoryCommand = new AsyncRelayCommand(PrintHistoryAsync);
            OpenRegisterChargeDialogCommand = new AsyncRelayCommand(OpenRegisterChargeDialog);
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
                IsPatientDataReadOnly = true;

                using (var scope = _serviceProvider.CreateScope())
                {
                    var clinicalEntryRepo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();
                    var paymentRepo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
                    var treatmentRepo = scope.ServiceProvider.GetRequiredService<ITreatmentRepository>();

                    var clinicalHistoryTask = clinicalEntryRepo.GetHistoryForPatientAsync(patient.Id);
                    var paymentHistoryTask = paymentRepo.GetPaymentsForPatientAsync(patient.Id);
                    var treatmentsTask = LoadAvailableTreatments(treatmentRepo); 

                    await Task.WhenAll(clinicalHistoryTask, paymentHistoryTask, treatmentsTask);

                    var clinicalHistory = (await clinicalHistoryTask).ToList();
                    var paymentHistory = (await paymentHistoryTask).ToList();

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

                LoadOdontogramStateFromJson();
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
                using (var scope = _serviceProvider.CreateScope())
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

            using (var scope = _serviceProvider.CreateScope())
            {
                var clinicalEntryRepo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();
                var paymentRepo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

                var clinicalHistoryTask = clinicalEntryRepo.GetHistoryForPatientAsync(CurrentPatient.Id);
                var paymentHistoryTask = paymentRepo.GetPaymentsForPatientAsync(CurrentPatient.Id);
                await Task.WhenAll(clinicalHistoryTask, paymentHistoryTask);

                var clinicalHistory = (await clinicalHistoryTask).ToList();
                var paymentHistory = (await paymentHistoryTask).ToList();

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

            ApplyFiltersAndPaging();
        }

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

            // *** CORRECCIÓN: Eliminada la llamada a _fileDialogService.ShowSaveDialog ***
            // Ya no preguntamos al usuario dónde guardar.

            try
            {
                string jsonState = JsonSerializer.Serialize(this.Odontogram); 

                // Usamos el _pdfService que inyectamos
                string generatedFilePath = await _pdfService.GenerateOdontogramPdfAsync(CurrentPatient, jsonState);

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
                using (var scope = _serviceProvider.CreateScope())
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

        partial void OnSelectedHistoryEntryChanged(ClinicalEntry? value)
        {
            DeleteClinicalEntryAsyncCommand.NotifyCanExecuteChanged();
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
                var vm = _serviceProvider.GetRequiredService<OdontogramViewModel>();
                var dialog = _serviceProvider.GetRequiredService<OdontogramWindow>();

                vm.LoadState(this.Odontogram, this.CurrentPatient);

                dialog.DataContext = vm;
                dialog.Owner = App.Current.MainWindow;

                vm.DialogResult = null; 
                dialog.ShowDialog(); 

                if (vm.DialogResult == true)
                {
                    var newJsonState = vm.GetSerializedState();
                    if (CurrentPatient.OdontogramStateJson != newJsonState)
                    {
                        CurrentPatient.OdontogramStateJson = newJsonState;
                        _ = SavePatientOdontogramStateAsync();
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
                using (var scope = _serviceProvider.CreateScope())
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
                _ = LoadPatient(CurrentPatient); 
            }
        }

        private async Task OpenRegisterChargeDialog()
        {
            if (CurrentPatient == null || _authService.CurrentUser == null) return;

            var dialog = _serviceProvider.GetRequiredService<ManualChargeDialog>();
            dialog.Owner = Application.Current.MainWindow;
            dialog.AvailableTreatments = this.AvailableTreatments; 

            if (dialog.ShowDialog() == true)
            {
                string concept = dialog.ManualConcept;
                decimal unitPrice = dialog.UnitPrice;
                int quantity = dialog.Quantity;
                int? treatmentId = dialog.SelectedTreatment?.Id;
                
                decimal totalCost = unitPrice * quantity;

                using (var scope = _serviceProvider.CreateScope())
                {
                    try
                    {
                        var clinicalRepo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();

                        var clinicalEntry = new ClinicalEntry
                        {
                            PatientId = CurrentPatient.Id,
                            DoctorId = _authService.CurrentUser.Id,
                            VisitDate = DateTime.Now,
                            Diagnosis = quantity > 1 ? $"{concept} (x{quantity})" : concept, 
                            TotalCost = totalCost, 
                        };

                        if (treatmentId.HasValue)
                        {
                            clinicalEntry.TreatmentsPerformed.Add(new ToothTreatment
                            {
                                ToothNumber = 0, // 0 = "N/A"
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

        private async Task SavePatientDataAsync()
        {
            if (CurrentPatient == null) return;
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var patientRepo = scope.ServiceProvider.GetRequiredService<IPatientRepository>();
                    var patientToUpdate = await patientRepo.GetByIdAsync(CurrentPatient.Id);
                    if (patientToUpdate != null)
                    {
                        patientToUpdate.Name = CurrentPatient.Name; 
                        patientToUpdate.Surname = CurrentPatient.Surname; 
                        patientToUpdate.DniNie = CurrentPatient.DniNie; 
                        patientToUpdate.Phone = CurrentPatient.Phone;
                        patientToUpdate.Address = CurrentPatient.Address;
                        patientToUpdate.Email = CurrentPatient.Email;
                        patientToUpdate.Notes = CurrentPatient.Notes;

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
                using (var scope = _serviceProvider.CreateScope())
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

            int maxItems = Math.Max(filteredClinical.Count, filteredPayments.Count);
            TotalPages = Math.Max(1, (int)Math.Ceiling((double)maxItems / PageSize));

            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var pagedClinical = filteredClinical
              .OrderByDescending(c => c.VisitDate) 
              .Skip((CurrentPage - 1) * PageSize)
              .Take(PageSize)
              .ToList();

            var pagedPayments = filteredPayments
              .OrderByDescending(p => p.PaymentDate) 
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