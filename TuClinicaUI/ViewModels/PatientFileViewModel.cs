using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.DataAccess.Repositories;
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
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeleteClinicalEntryCommand))] // Notifica al nuevo comando
        private ClinicalEntry? _selectedHistoryEntry;
        public ObservableCollection<Treatment> AvailableTreatments { get; } = new();
        [ObservableProperty]
        private Treatment? _selectedManualTreatment; // El tratamiento de la lista
        [ObservableProperty]
        private string _manualChargeConcept = string.Empty; // El concepto/diagnóstico

        [ObservableProperty]
        private decimal _manualChargePrice; // El precio
        public IAsyncRelayCommand DeleteClinicalEntryCommand { get; }
        public IAsyncRelayCommand RegisterManualChargeAsyncCommand { get; }


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

            // Escuchar mensajes del OdontogramViewModel
            WeakReferenceMessenger.Default.Register<RegisterTreatmentMessage>(this);
            DeleteClinicalEntryCommand = new AsyncRelayCommand(DeleteClinicalEntryAsync, () => SelectedHistoryEntry != null);
            RegisterManualChargeAsyncCommand = new AsyncRelayCommand(RegisterManualChargeAsync);
        }        
        


        private void InitializeOdontogram()
        {
            Odontogram.Clear();
            // Cuadrante 1 (11-18)
            for (int i = 18; i >= 11; i--) Odontogram.Add(new ToothViewModel(i));
            // Cuadrante 2 (21-28)
            for (int i = 21; i <= 28; i++) Odontogram.Add(new ToothViewModel(i));
            // Cuadrante 4 (41-48)
            for (int i = 48; i >= 41; i--) Odontogram.Add(new ToothViewModel(i));
            // Cuadrante 3 (31-38)
            for (int i = 31; i <= 38; i++) Odontogram.Add(new ToothViewModel(i));
        }

        /// <summary>
        /// Método principal para cargar o refrescar TODOS los datos de la ficha.
        /// </summary>
        // EN: TuClinicaUI/ViewModels/PatientFileViewModel.cs

        public async Task LoadPatient(Patient patient)
        {
            try
            {
                CurrentPatient = patient;

                // 1. Cargar historiales de BD (en paralelo)
                var clinicalHistoryTask = _clinicalEntryRepo.GetHistoryForPatientAsync(patient.Id);
                var paymentHistoryTask = _paymentRepo.GetPaymentsForPatientAsync(patient.Id);
                var treatmentsTask = LoadAvailableTreatments();

                await Task.WhenAll(clinicalHistoryTask, paymentHistoryTask, treatmentsTask);

                var clinicalHistory = (await clinicalHistoryTask).ToList();
                var paymentHistory = (await paymentHistoryTask).ToList();

                // 2. Cargar Colecciones de Historial (MÉTODO ROBUSTO)
                VisitHistory.Clear();
                foreach (var item in clinicalHistory)
                {
                    VisitHistory.Add(item);
                }

                PaymentHistory.Clear();
                foreach (var item in paymentHistory)
                {
                    PaymentHistory.Add(item);
                }

                // 3. Calcular resúmenes de saldo (Esto ya notifica a la UI)
                TotalCharged = VisitHistory.Sum(c => c.TotalCost);
                TotalPaid = PaymentHistory.Sum(p => p.Amount);
                CurrentBalance = TotalCharged - TotalPaid;

                // 4. Filtrar listas de acción (MÉTODO ROBUSTO)
                PendingCharges.Clear();
                var pending = VisitHistory.Where(c => c.Balance > 0).OrderBy(c => c.VisitDate);
                foreach (var item in pending)
                {
                    PendingCharges.Add(item);
                }

                UnallocatedPayments.Clear();
                var unallocated = PaymentHistory.Where(p => p.UnallocatedAmount > 0).OrderBy(p => p.PaymentDate);
                foreach (var item in unallocated)
                {
                    UnallocatedPayments.Add(item);
                }

                // 5. Calcular estado final del odontograma
                CalculateFinalOdontogramState();
            }
            catch (Exception ex)
            {
                // Si el refresco falla, mostramos un error
                _dialogService.ShowMessage($"Error crítico al refrescar la ficha del paciente: {ex.Message}", "Error de Carga");
            }
        }

        /// <summary>
        /// "Reproduce" todo el historial clínico para colorear el odontograma maestro.
        /// </summary>
        private void CalculateFinalOdontogramState()
        {
            // 1. Resetear odontograma a "Sano"
            foreach (var tooth in Odontogram)
            {
                tooth.FullStatus = ToothStatus.Sano;
                tooth.OclusalStatus = ToothStatus.Sano;
                // ... resetear todas las superficies
            }

            // 2. "Reproducir" historial
            var allTreatments = VisitHistory
                .SelectMany(v => v.TreatmentsPerformed)
                .OrderBy(t => t.ClinicalEntry.VisitDate); // Ordenar por fecha

            foreach (var treatment in allTreatments)
            {
                var toothVM = Odontogram.FirstOrDefault(t => t.ToothNumber == treatment.ToothNumber);
                if (toothVM != null)
                {
                    // Lógica simple: el último tratamiento gana
                    if (treatment.Surfaces.HasFlag(ToothSurface.Completo))
                        toothVM.FullStatus = treatment.TreatmentPerformed;
                    if (treatment.Surfaces.HasFlag(ToothSurface.Oclusal))
                        toothVM.OclusalStatus = treatment.TreatmentPerformed;
                    if (treatment.Surfaces.HasFlag(ToothSurface.Mesial))
                        toothVM.MesialStatus = treatment.TreatmentPerformed;
                    if (treatment.Surfaces.HasFlag(ToothSurface.Distal))
                        toothVM.DistalStatus = treatment.TreatmentPerformed;
                    if (treatment.Surfaces.HasFlag(ToothSurface.Vestibular))
                        toothVM.VestibularStatus = treatment.TreatmentPerformed;
                    if (treatment.Surfaces.HasFlag(ToothSurface.Lingual))
                        toothVM.LingualStatus = treatment.TreatmentPerformed;
                }
            }
        }

        /// <summary>
        /// Recibe el mensaje del OdontogramViewModel para crear un nuevo cargo.
        /// </summary>
        public async void Receive(RegisterTreatmentMessage message)
        {
            await RegisterTreatmentAsync(message.ToothNumber, message.Surface, message.Status, message.Price);
        }

        /// <summary>
        /// Lógica de negocio para guardar un nuevo tratamiento en la BD.
        /// </summary>
        private async Task RegisterTreatmentAsync(int toothNum, ToothSurface surface, ToothStatus status, decimal price)
        {
            if (CurrentPatient == null || _authService.CurrentUser == null) return;

            // ENVOLVEMOS TODA LA LÓGICA DE GUARDADO DENTRO DEL SCOPE
            using (var scope = _serviceProvider.CreateScope())
            {
                try
                {
                    // Obtenemos un repositorio "fresco"
                    var clinicalRepo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();

                    var toothTreatment = new ToothTreatment
                    {
                        ToothNumber = toothNum,
                        Surfaces = surface,
                        TreatmentPerformed = status,
                        Price = price
                    };

                    var clinicalEntry = new ClinicalEntry
                    {
                        PatientId = CurrentPatient.Id,
                        DoctorId = _authService.CurrentUser.Id,
                        VisitDate = DateTime.Now,
                        Diagnosis = $"Tratamiento: {status} en diente {toothNum}",
                        TotalCost = price,
                        TreatmentsPerformed = new List<ToothTreatment> { toothTreatment }
                    };

                    // Usamos el repositorio "fresco"
                    await clinicalRepo.AddAsync(clinicalEntry);
                    await clinicalRepo.SaveChangesAsync();

                    // Refrescamos la ficha
                    await LoadPatient(CurrentPatient);
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Error al registrar el tratamiento: {ex.Message}", "Error BD");
                }
            } // <-- El scope se cierra aquí, DESPUÉS de guardar.
        }

        /// <summary>
        /// COMANDO: Abre la ventana modal del odontograma.
        /// </summary>
        [RelayCommand]
        private void OpenOdontogramWindow()
        {
            try
            {
                var dialog = _serviceProvider.GetRequiredService<OdontogramWindow>();
                var vm = _serviceProvider.GetRequiredService<OdontogramViewModel>();

                // Pasamos el estado maestro al VM de la modal
                vm.LoadState(this.Odontogram);
                dialog.DataContext = vm;

                dialog.Owner = App.Current.MainWindow;
                dialog.ShowDialog();

                // NOTA: El refresco ocurre automáticamente cuando el VM recibe el mensaje.
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al abrir el odontograma: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// COMANDO: Registra un nuevo pago (Abono).
        /// </summary>
        [RelayCommand]
        private async Task RegisterNewPayment()
        {
            if (CurrentPatient == null) return;

            // 1. Pedir datos del pago
            var (ok, amount, method) = _dialogService.ShowNewPaymentDialog();
            if (!ok || amount <= 0) return;

            // 2. Crear el modelo
            var newPayment = new Payment
            {
                PatientId = CurrentPatient.Id,
                PaymentDate = DateTime.Now,
                Amount = amount,
                Method = method
            };

            // 3. Guardar en BD
            try
            {
                await _paymentRepo.AddAsync(newPayment);
                await _paymentRepo.SaveChangesAsync();

                // 4. Refrescar ficha
                await LoadPatient(CurrentPatient);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al guardar el pago: {ex.Message}", "Error BD");
            }
        }

        /// <summary>
        /// COMANDO: Asigna un pago a un cargo.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanAllocate))]
        private async Task AllocatePayment()
        {
            if (SelectedCharge == null || SelectedPayment == null || AmountToAllocate <= 0) return;

            // 1. Validar que el monto no exceda lo disponible
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

            // 2. Crear la asignación
            var allocation = new PaymentAllocation
            {
                PaymentId = SelectedPayment.Id,
                ClinicalEntryId = SelectedCharge.Id,
                AmountAllocated = AmountToAllocate
            };

            // 3. Guardar en BD
            try
            {
                await _allocationRepo.AddAsync(allocation);
                await _allocationRepo.SaveChangesAsync();

                // 4. Refrescar ficha
                await LoadPatient(CurrentPatient);

                // 5. Limpiar selección
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

        /// <summary>
        /// Se dispara cuando cambia SelectedCharge o SelectedPayment.
        /// </summary>
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

        /// <summary>
        /// Rellena automáticamente el campo "Monto a Asignar" con el valor
        /// más pequeño entre el saldo del cargo y el monto no asignado del pago.
        /// </summary>
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

        [ObservableProperty]
        private bool _isPatientDataReadOnly = true;

        [RelayCommand]
        private void ToggleEditPatientData()
        {
            IsPatientDataReadOnly = !IsPatientDataReadOnly;
        }

        // ¡¡QUITA el atributo [RelayCommand] de esta línea!!
        private async Task RegisterManualChargeAsync()
        {
            if (CurrentPatient == null || _authService.CurrentUser == null) return;

            if (string.IsNullOrWhiteSpace(ManualChargeConcept) || ManualChargePrice <= 0)
            {
                _dialogService.ShowMessage("Debe introducir un concepto y un precio mayor que cero.", "Datos incompletos");
                return;
            }

            // Esta es la lógica de "scope" que asegura que el guardado funcione
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
                // Creamos un "scope" temporal para obtener servicios Scoped (como el repositorio)
                // desde nuestro servicio Singleton (este ViewModel).
                using (var scope = _serviceProvider.CreateScope())
                {
                    var patientRepo = scope.ServiceProvider.GetRequiredService<IPatientRepository>();

                    // Obtenemos la entidad original de la BD para que EF Core la rastree
                    var patientToUpdate = await patientRepo.GetByIdAsync(CurrentPatient.Id);
                    if (patientToUpdate != null)
                    {
                        // Copiamos los valores editados desde CurrentPatient (que está bindeado a la UI)
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

        /// <summary>
        /// Auto-rellena los campos de texto cuando se selecciona un tratamiento de la lista.
        /// </summary>
        partial void OnSelectedManualTreatmentChanged(Treatment? value)
        {
            if (value != null)
            {
                ManualChargeConcept = value.Name;
                ManualChargePrice = value.DefaultPrice;
            }
        }
        // --- AÑADE ESTE MÉTODO NUEVO ---
        private async Task DeleteClinicalEntryAsync()
        {
            if (SelectedHistoryEntry == null) return;
            if (CurrentPatient == null) return;

            // 1. Pedir confirmación (¡MUY IMPORTANTE!)
            var result = _dialogService.ShowConfirmation(
                $"¿Está seguro de que desea eliminar permanentemente este cargo?" +
                $"\n\nConcepto: {SelectedHistoryEntry.Diagnosis}" +
                $"\nCoste: {SelectedHistoryEntry.TotalCost:C}" +
                $"\n\nCualquier pago asignado a este cargo será des-asignado.",
                "Confirmar Eliminación de Cargo");

            if (result == CoreDialogResult.No) return;

            try
            {
                // 2. Llamar al repositorio para hacer el borrado transaccional
                bool success = await _clinicalEntryRepo.DeleteEntryAndAllocationsAsync(SelectedHistoryEntry.Id);

                if (success)
                {
                    _dialogService.ShowMessage("Cargo eliminado correctamente.", "Éxito");
                }
                else
                {
                    _dialogService.ShowMessage("No se pudo eliminar el cargo (quizás ya estaba borrado).", "Error");
                }

                // 3. Refrescar TODA la ficha (Saldos, Listas Pendientes, etc.)
                await LoadPatient(CurrentPatient);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al eliminar el cargo: {ex.Message}", "Error de Base de Datos");
            }
        }
    }
}