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

        // --- Estado Maestro ---
        [ObservableProperty]
        private Patient? _currentPatient;

        // El estado "Maestro" y final del odontograma
        public ObservableCollection<ToothViewModel> Odontogram { get; } = new();

        // --- Colecciones de Historial (Pestañas inferiores) ---
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

        // --- Colecciones de Acción (Panel de Facturación) ---
        [ObservableProperty]
        private ObservableCollection<ClinicalEntry> _pendingCharges = new();
        [ObservableProperty]
        private ObservableCollection<Payment> _unallocatedPayments = new();

        // --- Estado de Asignación (Panel de Facturación) ---
        [ObservableProperty]
        private ClinicalEntry? _selectedCharge; // Cargo pendiente seleccionado
        [ObservableProperty]
        private Payment? _selectedPayment; // Pago no asignado seleccionado
        [ObservableProperty]
        private decimal _amountToAllocate; // Monto a asignar

        // REINCORPORACIÓN MANUAL DE PROPIEDAD Y SU CAMBIO DE ESTADO
        [ObservableProperty]
        private ClinicalEntry? _selectedHistoryEntry;

        public ObservableCollection<Treatment> AvailableTreatments { get; } = new();
        [ObservableProperty]
        private Treatment? _selectedManualTreatment; // El tratamiento de la lista
        [ObservableProperty]
        private string _manualChargeConcept = string.Empty; // El concepto/diagnóstico

        [ObservableProperty]
        private decimal _manualChargePrice; // El precio

        // --- DECLARACIÓN MANUAL DE COMANDOS PROBLEMATICOS (Debe existir) ---
        public IAsyncRelayCommand RegisterManualChargeAsyncCommand { get; }
        public IAsyncRelayCommand DeleteClinicalEntryAsyncCommand { get; }
        public IRelayCommand PreviousPageCommand { get; }
        public IRelayCommand NextPageCommand { get; }
        public IRelayCommand ToggleEditPatientDataCommand { get; }

        [ObservableProperty]
        private bool _hasNextPage;


        [ObservableProperty]
        private bool _isPatientDataReadOnly = true;

        // --- BÚSQUEDA Y PAGINACIÓN ---
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

        // --- ODONTOGRAMA PREVIEW ---
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
          ITreatmentRepository treatmentRepository)
        {
            _clinicalEntryRepo = clinicalEntryRepo;
            _paymentRepo = paymentRepo;
            _allocationRepo = allocationRepo;
            _authService = authService;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            _treatmentRepository = treatmentRepository;

            // Inicializar el odontograma maestro con 32 dientes
            InitializeOdontogram();

            // Registrar mensajes
            WeakReferenceMessenger.Default.Register<OpenOdontogramMessage>(this, (r, m) => OpenOdontogramWindow());
            WeakReferenceMessenger.Default.Register<RegisterTreatmentMessage>(this);

            // --- INICIALIZACIÓN MANUAL (Debe existir) ---
            DeleteClinicalEntryAsyncCommand = new AsyncRelayCommand(DeleteClinicalEntryAsync, CanDeleteClinicalEntry);
            RegisterManualChargeAsyncCommand = new AsyncRelayCommand(RegisterManualChargeAsync);
            PreviousPageCommand = new RelayCommand(PreviousPage, () => CurrentPage > 1);
            NextPageCommand = new RelayCommand(NextPage, () => CurrentPage < TotalPages);
            ToggleEditPatientDataCommand = new RelayCommand(ToggleEditPatientData);
            // --- FIN INICIALIZACIÓN MANUAL ---
        }

        private void InitializeOdontogram()
        {
            Odontogram.Clear();
            // 1. Cuadrante 1: 18 a 11
            for (int i = 18; i >= 11; i--) Odontogram.Add(new ToothViewModel(i));
            // 2. Cuadrante 2: 21 a 28
            for (int i = 21; i <= 28; i++) Odontogram.Add(new ToothViewModel(i));
            // 3. Cuadrante 4: 41 a 48
            for (int i = 41; i <= 48; i++) Odontogram.Add(new ToothViewModel(i));
            // 4. Cuadrante 3: 38 a 31
            for (int i = 38; i >= 31; i--) Odontogram.Add(new ToothViewModel(i));
        }

        /// <summary>
        /// Método principal para cargar o refrescar TODOS los datos de la ficha.
        /// </summary>
        public async Task LoadPatient(Patient patient)
        {
            

            if (_isLoading)
            {
               
                return;
            }

            try
            {
                _isLoading = true;
                

                CurrentPatient = patient;
               

                var clinicalHistoryTask = _clinicalEntryRepo.GetHistoryForPatientAsync(patient.Id);
                var paymentHistoryTask = _paymentRepo.GetPaymentsForPatientAsync(patient.Id);
                var treatmentsTask = LoadAvailableTreatments();

                
                await Task.WhenAll(clinicalHistoryTask, paymentHistoryTask, treatmentsTask);
                

                var clinicalHistory = (await clinicalHistoryTask).ToList();
                var paymentHistory = (await paymentHistoryTask).ToList();
                

                // ... El resto del código lo dejamos igual por ahora ...
                // Si el error aparece después de este punto, me lo dices.

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

        partial void OnCurrentPatientChanged(Patient? value)
        {
            //if (value != null)
           // {
            //    _ = LoadPatient(value);
            //}
        }

        /// <summary>
        /// "Reproduce" todo el historial clínico para colorear el odontograma maestro.
        /// </summary>
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

        // CORRECCIÓN CS0103: Descomentar y usar el nombre del comando correcto.
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

                    await LoadPatient(CurrentPatient);
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
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al abrir el odontograma: {ex.Message}", "Error");
            }
        }

        [RelayCommand]
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
                await _paymentRepo.AddAsync(newPayment);
                await _paymentRepo.SaveChangesAsync();
                await LoadPatient(CurrentPatient);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al guardar el pago: {ex.Message}", "Error BD");
            }
        }

        [RelayCommand(CanExecute = nameof(CanAllocate))]
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
                await _allocationRepo.AddAsync(allocation);
                await _allocationRepo.SaveChangesAsync();
                await LoadPatient(CurrentPatient);

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

        partial void OnSelectedChargeChanged(ClinicalEntry? value)
        {
            AutoFillAmountToAllocate();
            AllocatePaymentCommand.NotifyCanExecuteChanged();
            DeleteClinicalEntryAsyncCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedPaymentChanged(Payment? value)
        {
            AutoFillAmountToAllocate();
            AllocatePaymentCommand.NotifyCanExecuteChanged();
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

                    var clinicalEntry = new ClinicalEntry
                    {
                        PatientId = CurrentPatient.Id,
                        DoctorId = _authService.CurrentUser.Id,
                        VisitDate = DateTime.Now,
                        Diagnosis = ManualChargeConcept,
                        TotalCost = ManualChargePrice,
                    };

                    await clinicalRepo.AddAsync(clinicalEntry);
                    await clinicalRepo.SaveChangesAsync();

                    ManualChargeConcept = string.Empty;
                    ManualChargePrice = 0;
                    SelectedManualTreatment = null;

                    await LoadPatient(CurrentPatient);
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

        [RelayCommand]
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
                        patientToUpdate.Phone = CurrentPatient.Phone;
                        patientToUpdate.Address = CurrentPatient.Address;
                        patientToUpdate.Email = CurrentPatient.Email;
                        patientToUpdate.Notes = CurrentPatient.Notes;

                        patientRepo.Update(patientToUpdate);
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
                await LoadPatient(CurrentPatient);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al eliminar el cargo: {ex.Message}", "Error de Base de Datos");
            }
        }

        private bool CanDeleteClinicalEntry()
        {
            return SelectedHistoryEntry != null;
        }

        // ====================================================================
        // BÚSQUEDA Y PAGINACIÓN
        // ====================================================================


        private void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                PreviousPageCommand.NotifyCanExecuteChanged();
                NextPageCommand.NotifyCanExecuteChanged();
            }
        }


        private void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                PreviousPageCommand.NotifyCanExecuteChanged();
                NextPageCommand.NotifyCanExecuteChanged();
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFiltersAndPaging();
        }

        partial void OnCurrentPageChanged(int value)
        {
            ApplyFiltersAndPaging();
        }
        partial void OnTotalPagesChanged(int value)
        {
            HasNextPage = CurrentPage < TotalPages;
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

            TotalPages = Math.Max(1, (int)Math.Ceiling((double)filteredClinical.Count / PageSize));
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var pagedClinical = filteredClinical
              .Skip((CurrentPage - 1) * PageSize)
              .Take(PageSize)
              .ToList();

            var pagedPayments = filteredPayments
              .Skip((CurrentPage - 1) * PageSize)
              .Take(PageSize)
              .ToList();

            FilteredVisitHistory.Clear();
            foreach (var item in pagedClinical) FilteredVisitHistory.Add(item);

            FilteredPaymentHistory.Clear();
            foreach (var item in pagedPayments) FilteredPaymentHistory.Add(item);

            PreviousPageCommand.NotifyCanExecuteChanged();
            NextPageCommand.NotifyCanExecuteChanged();
        }
    }
}