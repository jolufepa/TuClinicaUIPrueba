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
using System.IO;
using TuClinica.UI.ViewModels.Events;

namespace TuClinica.UI.ViewModels
{
    public partial class PatientFileViewModel : BaseViewModel
    {
        // --- Servicios ---
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IFileDialogService _fileDialogService;
        private readonly IValidationService _validationService; // *** AÑADIDO (Corrige error CS0103) ***

        // *** NUEVO: Para guardar el estado original del paciente al iniciar la edición ***
        private Patient? _originalPatientState;

        // --- Estado Maestro ---
        [ObservableProperty]
        private Patient? _currentPatient;
        public ObservableCollection<ToothViewModel> Odontogram { get; } = new();

        // --- Colecciones de Historial (MAESTRAS) ---
        [ObservableProperty]
        private ObservableCollection<ClinicalEntry> _visitHistory = new();
        [ObservableProperty]
        private ObservableCollection<Payment> _paymentHistory = new();

        // --- NUEVA COLECCIÓN PARA LA BITÁCORA (UI) ---
        [ObservableProperty]
        private ObservableCollection<HistorialEventBase> _historialCombinado = new();

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

        public ObservableCollection<Treatment> AvailableTreatments { get; } = new();

        // --- DECLARACIÓN MANUAL DE COMANDOS CRÍTICOS ---
        public IAsyncRelayCommand<ClinicalEntry> DeleteClinicalEntryAsyncCommand { get; }
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
        [NotifyCanExecuteChangedFor(nameof(SavePatientDataAsyncCommand))] // Notifica al comando si cambia
        private bool _isPatientDataReadOnly = true;
        [ObservableProperty]
        private OdontogramPreviewViewModel _odontogramPreviewVM = new();
        [ObservableProperty]
        private bool _isLoading = false;

        // --- CONSTRUCTOR MODIFICADO ---
        public PatientFileViewModel(
          IAuthService authService,
          IDialogService dialogService,
          IServiceProvider serviceProvider,
          IFileDialogService fileDialogService,
          IValidationService validationService) // *** AÑADIDO ***
        {
            _authService = authService;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            _fileDialogService = fileDialogService;
            _validationService = validationService; // *** AÑADIDO ***

            InitializeOdontogram();
            WeakReferenceMessenger.Default.Register<OpenOdontogramMessage>(this, (r, m) => OpenOdontogramWindow());

            // --- INICIALIZACIÓN MANUAL DE COMANDOS ---
            DeleteClinicalEntryAsyncCommand = new AsyncRelayCommand<ClinicalEntry>(DeleteClinicalEntryAsync, CanDeleteClinicalEntry);
            ToggleEditPatientDataCommand = new RelayCommand(ToggleEditPatientData);
            SavePatientDataAsyncCommand = new AsyncRelayCommand(SavePatientDataAsync, CanSavePatientData); // *** CAMBIO ***
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
                CurrentPatient = patient; // Esto dispara OnCurrentPatientChanged
                IsPatientDataReadOnly = true;

                // Carga inicial de datos (sin cambios)
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

                    // Sigue poblando las listas maestras
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
                SavePatientDataAsyncCommand.NotifyCanExecuteChanged(); // Refrescar el estado del botón al cargar
            }
        }

        // *** MÉTODO PARCIAL AÑADIDO: Se activa cuando CurrentPatient cambia ***
        partial void OnCurrentPatientChanged(Patient? oldValue, Patient? newValue)
        {
            // Desuscribirse del paciente anterior
            if (oldValue != null)
            {
                oldValue.PropertyChanged -= CurrentPatient_PropertyChanged;
            }

            // Suscribirse al nuevo paciente
            if (newValue != null)
            {
                newValue.PropertyChanged += CurrentPatient_PropertyChanged;
                _originalPatientState = newValue.DeepCopy(); // Guardar copia original
            }
            else
            {
                _originalPatientState = null;
            }

            SavePatientDataAsyncCommand.NotifyCanExecuteChanged(); // Reevaluar al cambiar de paciente
        }

        // *** MÉTODO AÑADIDO: Escucha los cambios en las propiedades del paciente ***
        private void CurrentPatient_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Si no estamos en modo edición, ignorar
            if (IsPatientDataReadOnly) return;

            // Si una propiedad cambia, reevaluar si el botón Guardar debe estar activo
            SavePatientDataAsyncCommand.NotifyCanExecuteChanged();
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

            List<ClinicalEntry> clinicalHistory;
            List<Payment> paymentHistory;

            // 1. Recargar datos de la BD
            using (var scope = _serviceProvider.CreateScope())
            {
                var clinicalEntryRepo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();
                var paymentRepo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

                var clinicalHistoryTask = clinicalEntryRepo.GetHistoryForPatientAsync(CurrentPatient.Id);
                var paymentHistoryTask = paymentRepo.GetPaymentsForPatientAsync(CurrentPatient.Id);
                await Task.WhenAll(clinicalHistoryTask, paymentHistoryTask);

                clinicalHistory = (await clinicalHistoryTask).ToList();
                paymentHistory = (await paymentHistoryTask).ToList();

                // 2. Actualizar colecciones maestras
                VisitHistory.Clear();
                PaymentHistory.Clear();
                clinicalHistory.ForEach(VisitHistory.Add);
                paymentHistory.ForEach(PaymentHistory.Add);
            }

            // 3. Calcular totales
            TotalCharged = VisitHistory.Sum(c => c.TotalCost);
            TotalPaid = PaymentHistory.Sum(p => p.Amount);
            CurrentBalance = TotalCharged - TotalPaid;

            // 4. Actualizar pestaña "Facturación" (aunque esté oculta, la lógica puede ser útil)
            PendingCharges.Clear();
            VisitHistory.Where(c => c.Balance > 0).OrderBy(c => c.VisitDate).ToList().ForEach(PendingCharges.Add);
            UnallocatedPayments.Clear();
            PaymentHistory.Where(p => p.UnallocatedAmount > 0).OrderBy(p => p.PaymentDate).ToList().ForEach(UnallocatedPayments.Add);

            // --- 5. Poblar la Bitácora ---
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

        // --- 5. MÉTODO MODIFICADO (PrintHistoryAsync) ---
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

                // 1. Crear un ámbito para resolver el IPdfService
                using (var scope = _serviceProvider.CreateScope())
                {
                    var pdfService = scope.ServiceProvider.GetRequiredService<IPdfService>();

                    // 2. Llamar al servicio para generar el PDF en memoria
                    pdfBytes = await pdfService.GenerateHistoryPdfAsync(
                        CurrentPatient,
                        VisitHistory.ToList(),      // Pasa la lista de cargos
                        PaymentHistory.ToList(),    // Pasa la lista de pagos
                        CurrentBalance              // Pasa el saldo actual
                    );
                } // El ámbito y el pdfService se destruyen aquí

                // 3. Crear un nombre de archivo temporal único
                string tempFileName = $"Historial_{CurrentPatient.Surname}_{CurrentPatient.Name}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                string tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);

                // 4. Guardar los bytes en ese archivo temporal
                await File.WriteAllBytesAsync(tempFilePath, pdfBytes);

                // 5. Abrir el archivo con el visor de PDF predeterminado
                Process.Start(new ProcessStartInfo(tempFilePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Se produjo un error al generar o abrir el PDF del historial:\n{ex.Message}", "Error de Impresión");
            }
        }

        // --- 6. MÉTODO MODIFICADO (PrintOdontogramAsync) ---
        private async Task PrintOdontogramAsync()
        {
            if (CurrentPatient == null) return;

            try
            {
                string jsonState = JsonSerializer.Serialize(this.Odontogram);
                string generatedFilePath;

                // 1. Crear ámbito
                using (var scope = _serviceProvider.CreateScope())
                {
                    var pdfService = scope.ServiceProvider.GetRequiredService<IPdfService>();
                    // 2. Generar PDF
                    generatedFilePath = await pdfService.GenerateOdontogramPdfAsync(CurrentPatient, jsonState);
                } // Ámbito destruido

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

        private async void OpenOdontogramWindow()
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

            if (!IsPatientDataReadOnly)
            {
                // ACABAMOS DE ENTRAR EN MODO EDICIÓN
                if (CurrentPatient != null)
                {
                    _originalPatientState = CurrentPatient.DeepCopy();
                }
            }
            else
            {
                // ACABAMOS DE SALIR DE MODO EDICIÓN (Cancelando)
                if (CurrentPatient != null && _originalPatientState != null)
                {
                    CurrentPatient.CopyFrom(_originalPatientState); // Restaurar datos
                }
                _originalPatientState = null;
            }
            // Notificar al comando de guardar para que se active/desactive
            SavePatientDataAsyncCommand.NotifyCanExecuteChanged();
        }

        private async Task OpenRegisterChargeDialog()
        {
            if (CurrentPatient == null || _authService.CurrentUser == null) return;

            var dialog = _serviceProvider.GetRequiredService<ManualChargeDialog>();

            Window? owner = Application.Current.MainWindow;
            if (owner != null && owner != dialog)
            {
                dialog.Owner = owner;
            }

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

        // *** NUEVO: Método CanExecute para SavePatientDataAsyncCommand ***
        private bool CanSavePatientData()
        {
            // Solo activo si estamos en modo edición (IsPatientDataReadOnly es false)
            // Y si realmente ha habido cambios
            return !IsPatientDataReadOnly && HasPatientDataChanged();
        }

        // *** NUEVO: Método para detectar si el paciente ha cambiado ***
        private bool HasPatientDataChanged()
        {
            if (_originalPatientState == null || CurrentPatient == null) return false;

            // Comparar todas las propiedades relevantes
            return _originalPatientState.Name != CurrentPatient.Name ||
                   _originalPatientState.Surname != CurrentPatient.Surname ||
                   _originalPatientState.DniNie != CurrentPatient.DniNie ||
                   _originalPatientState.DateOfBirth != CurrentPatient.DateOfBirth ||
                   _originalPatientState.Phone != CurrentPatient.Phone ||
                   _originalPatientState.Address != CurrentPatient.Address ||
                   _originalPatientState.Email != CurrentPatient.Email ||
                   _originalPatientState.Notes != CurrentPatient.Notes;
        }

        private async Task SavePatientDataAsync()
        {
            if (CurrentPatient == null) return;

            // Normalización
            CurrentPatient.Name = ToTitleCase(CurrentPatient.Name);
            CurrentPatient.Surname = ToTitleCase(CurrentPatient.Surname);
            CurrentPatient.DniNie = CurrentPatient.DniNie?.ToUpper().Trim() ?? string.Empty;
            CurrentPatient.Email = CurrentPatient.Email?.ToLower().Trim() ?? string.Empty;

            // Validación Formato
            if (!_validationService.IsValidDniNie(CurrentPatient.DniNie))
            {
                _dialogService.ShowMessage("El DNI o NIE introducido no tiene un formato válido.", "DNI/NIE Inválido");
                return;
            }

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var patientRepo = scope.ServiceProvider.GetRequiredService<IPatientRepository>();
                    var patientToUpdate = await patientRepo.GetByIdAsync(CurrentPatient.Id);
                    if (patientToUpdate != null)
                    {
                        // Aplicar los cambios
                        patientToUpdate.CopyFrom(CurrentPatient);

                        await patientRepo.SaveChangesAsync();
                        _dialogService.ShowMessage("Datos del paciente actualizados.", "Éxito");
                        _originalPatientState = CurrentPatient.DeepCopy(); // Actualizar el estado original
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
                // Esto deshabilitará el botón Guardar
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

        // Método de Ayuda
        private string ToTitleCase(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower());
        }
    }
}