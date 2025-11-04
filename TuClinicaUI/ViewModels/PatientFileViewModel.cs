using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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

        // DECLARACIÓN MANUAL DE COMANDOS
        public IAsyncRelayCommand DeleteClinicalEntryCommand { get; }
        public IAsyncRelayCommand RegisterManualChargeAsyncCommand { get; }

        [ObservableProperty]
        private bool _isPatientDataReadOnly = true;


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

            // INICIALIZACIÓN MANUAL DE COMANDOS (para mantener la estructura manual)
            DeleteClinicalEntryCommand = new AsyncRelayCommand(DeleteClinicalEntryAsync, CanDeleteClinicalEntry);
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
        /// (Lógica corregida para usar Condition y Restoration y evitar CS1061)
        /// </summary>
        private void CalculateFinalOdontogramState()
        {
            // 1. Resetear odontograma a "Sano" (Condición) y "Ninguna" (Restauración)
            foreach (var tooth in Odontogram)
            {
                // Resetear Condición
                tooth.FullCondition = ToothCondition.Sano;
                tooth.OclusalCondition = ToothCondition.Sano;
                tooth.MesialCondition = ToothCondition.Sano;
                tooth.DistalCondition = ToothCondition.Sano;
                tooth.VestibularCondition = ToothCondition.Sano;
                tooth.LingualCondition = ToothCondition.Sano;

                // Resetear Restauración
                tooth.FullRestoration = ToothRestoration.Ninguna;
                tooth.OclusalRestoration = ToothRestoration.Ninguna;
                tooth.MesialRestoration = ToothRestoration.Ninguna;
                tooth.DistalRestoration = ToothRestoration.Ninguna;
                tooth.VestibularRestoration = ToothRestoration.Ninguna;
                tooth.LingualRestoration = ToothRestoration.Ninguna;
            }

            // 2. "Reproducir" historial
            var allTreatments = VisitHistory
                .SelectMany(v => v.TreatmentsPerformed)
                .OrderBy(t => t.ClinicalEntry!.VisitDate); // Usar ClinicalEntry! si es seguro que no es null

            foreach (var treatment in allTreatments)
            {
                var toothVM = Odontogram.FirstOrDefault(t => t.ToothNumber == treatment.ToothNumber);
                if (toothVM == null) continue;

                // **Lógica de mapeo TEMPORAL para COMPILACIÓN y estructura inicial**
                // Asume que si hay un TreatmentId, el resultado es una Obturación.
                ToothRestoration restoredState = MapTreatmentToRestoration(treatment.TreatmentId);

                // Aplicar la restauración (Obturación, Corona, etc.)
                ApplyRestorationToToothVM(toothVM, treatment.Surfaces, restoredState);

                // Si el tratamiento es una restauración, asumimos que elimina la patología.
                if (restoredState != ToothRestoration.Ninguna)
                {
                    ApplyConditionToToothVM(toothVM, treatment.Surfaces, ToothCondition.Sano);
                }
            }
        }

        // --- FUNCIONES AUXILIARES DE MAPEO Y APLICACIÓN ---

        /// <summary>
        /// Función temporal para mapear TreatmentId a ToothRestoration.
        /// </summary>
        private ToothRestoration MapTreatmentToRestoration(int treatmentId)
        {
            // Lógica simple: Si existe ID, es una Obturación para fines de compilación.
            if (treatmentId > 0) return ToothRestoration.Obturacion;
            return ToothRestoration.Ninguna;
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
        // -----------------------------------------------------------------------------------

        partial void OnSelectedHistoryEntryChanged(ClinicalEntry? value)
        {
            // Notificar que la habilidad para ejecutar el comando ha cambiado
            DeleteClinicalEntryCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Recibe el mensaje del OdontogramViewModel para crear un nuevo cargo.
        /// (MODIFICADO para recibir TreatmentId y RestorationResult)
        /// </summary>
        public async void Receive(RegisterTreatmentMessage message)
        {
            // Llama a la lógica de guardado con los nuevos campos
            await RegisterTreatmentAsync(
                message.ToothNumber,
                message.Surface,
                message.TreatmentId,
                message.RestorationResult,
                message.Price);
        }

        /// <summary>
        /// Lógica de negocio para guardar un nuevo tratamiento en la BD.
        /// (MODIFICADO para usar TreatmentId del catálogo)
        /// </summary>
        private async Task RegisterTreatmentAsync(
            int toothNum,
            ToothSurface surface,
            int treatmentId, // <--- ID del catálogo de Treatment
            ToothRestoration restorationResult, // <--- Resultado visual
            decimal price)
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
                    // Obtener un repositorio "fresco"
                    var clinicalRepo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();

                    var toothTreatment = new ToothTreatment
                    {
                        ToothNumber = toothNum,
                        Surfaces = surface,
                        TreatmentId = selectedTreatment.Id, // <--- USA EL ID DEL CATÁLOGO
                        Price = price
                    };

                    var clinicalEntry = new ClinicalEntry
                    {
                        PatientId = CurrentPatient.Id,
                        DoctorId = _authService.CurrentUser.Id,
                        VisitDate = DateTime.Now,
                        // Usa el nombre del Treatment para el Diagnosis (Resuelve CS1061)
                        Diagnosis = $"Tratamiento: {selectedTreatment.Name} en diente {toothNum} ({surface})",
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
            }
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
            DeleteClinicalEntryCommand.NotifyCanExecuteChanged();
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

        [RelayCommand]
        private void ToggleEditPatientData()
        {
            IsPatientDataReadOnly = !IsPatientDataReadOnly;
        }

        [RelayCommand]
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

        // LÓGICA MANUAL DE COMANDO (PARA EVITAR PROBLEMAS CON RELAYCOMMAND)
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
                // Se asume que IClinicalEntryRepository tiene este método
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

        // FUNCIÓN CanExecute REQUERIDA POR EL COMANDO MANUAL
        private bool CanDeleteClinicalEntry()
        {
            return SelectedHistoryEntry != null;
        }
    }
}