using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.DataAccess;
using TuClinica.UI.Messages;
using TuClinica.UI.ViewModels.Events;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;

namespace TuClinica.UI.ViewModels
{
    public partial class PatientFinancialViewModel : BaseViewModel
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDialogService _dialogService;
        private readonly IAuthService _authService;
        private readonly IPdfService _pdfService;

        [ObservableProperty]
        private Patient? _currentPatient;

        // --- Colecciones ---
        [ObservableProperty]
        private ObservableCollection<ClinicalEntry> _visitHistory = new();
        [ObservableProperty]
        private ObservableCollection<Payment> _paymentHistory = new();
        [ObservableProperty]
        private ObservableCollection<HistorialEventBase> _historialCombinado = new();

        [ObservableProperty]
        private ObservableCollection<Treatment> _availableTreatments = new();

        // --- Propiedades de Facturación ---
        [ObservableProperty] private decimal _totalCharged;
        [ObservableProperty] private decimal _totalPaid;
        [ObservableProperty] private decimal _currentBalance;

        [ObservableProperty] private ObservableCollection<ClinicalEntry> _pendingCharges = new();
        [ObservableProperty] private ObservableCollection<Payment> _unallocatedPayments = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAllocate))]
        private ClinicalEntry? _selectedCharge;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAllocate))]
        private Payment? _selectedPayment;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AllocatePaymentCommand))]
        private decimal _amountToAllocate;

        public bool CanAllocate => SelectedCharge != null && SelectedPayment != null && AmountToAllocate > 0;

        // --- Comandos ---
        public IAsyncRelayCommand<ClinicalEntry> DeleteClinicalEntryAsyncCommand { get; }
        public IAsyncRelayCommand<Payment> DeletePaymentAsyncCommand { get; }
        public IAsyncRelayCommand AllocatePaymentCommand { get; }
        public IAsyncRelayCommand RegisterNewPaymentCommand { get; }
        public IRelayCommand NewBudgetCommand { get; }
        public IAsyncRelayCommand PrintHistoryCommand { get; }
        public IAsyncRelayCommand OpenRegisterChargeDialogCommand { get; }

        public PatientFinancialViewModel(
            IServiceScopeFactory scopeFactory,
            IDialogService dialogService,
            IAuthService authService,
            IPdfService pdfService)
        {
            _scopeFactory = scopeFactory;
            _dialogService = dialogService;
            _authService = authService;
            _pdfService = pdfService;

            DeleteClinicalEntryAsyncCommand = new AsyncRelayCommand<ClinicalEntry>(DeleteClinicalEntryAsync, e => e != null);
            DeletePaymentAsyncCommand = new AsyncRelayCommand<Payment>(DeletePaymentAsync, p => p != null);
            AllocatePaymentCommand = new AsyncRelayCommand(AllocatePayment, () => CanAllocate);
            RegisterNewPaymentCommand = new AsyncRelayCommand(RegisterNewPayment);
            NewBudgetCommand = new RelayCommand(NewBudget);
            PrintHistoryCommand = new AsyncRelayCommand(PrintHistoryAsync);
            OpenRegisterChargeDialogCommand = new AsyncRelayCommand(OpenRegisterChargeDialog);
        }

        // --- Métodos de Notificación ---
        partial void OnSelectedChargeChanged(ClinicalEntry? value) { AutoFillAmountToAllocate(); AllocatePaymentCommand.NotifyCanExecuteChanged(); }
        partial void OnSelectedPaymentChanged(Payment? value) { AutoFillAmountToAllocate(); AllocatePaymentCommand.NotifyCanExecuteChanged(); }
        partial void OnAmountToAllocateChanged(decimal value) => AllocatePaymentCommand.NotifyCanExecuteChanged();

        public async Task LoadAsync(Patient patient, CancellationToken token)
        {
            CurrentPatient = patient;
            SelectedCharge = null;
            SelectedPayment = null;
            AmountToAllocate = 0;

            // Cargar tratamientos disponibles (para el diálogo de cargos)
            using (var scope = _scopeFactory.CreateScope())
            {
                var treatmentRepo = scope.ServiceProvider.GetRequiredService<ITreatmentRepository>();
                await LoadAvailableTreatments(treatmentRepo, token);
            }

            await RefreshBillingCollections(token);
        }

        private async Task LoadAvailableTreatments(ITreatmentRepository r, CancellationToken t)
        {
            AvailableTreatments.Clear();
            var ts = await r.GetAllAsync();
            t.ThrowIfCancellationRequested();
            foreach (var tr in ts.Where(x => x.IsActive).OrderBy(x => x.Name))
                AvailableTreatments.Add(tr);
        }

        private async Task RefreshBillingCollections(CancellationToken token = default)
        {
            if (CurrentPatient == null) return;

            IEnumerable<ClinicalEntry> clinicalHistory;
            IEnumerable<Payment> paymentHistory;
            decimal charged, paid;

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

            VisitHistory.Clear();
            foreach (var c in clinicalHistory) VisitHistory.Add(c);

            PaymentHistory.Clear();
            foreach (var p in paymentHistory) PaymentHistory.Add(p);

            TotalCharged = charged;
            TotalPaid = paid;
            CurrentBalance = TotalCharged - TotalPaid;

            PendingCharges.Clear();
            foreach (var c in VisitHistory.Where(c => c.Balance > 0).OrderBy(c => c.VisitDate)) PendingCharges.Add(c);

            UnallocatedPayments.Clear();
            foreach (var p in PaymentHistory.Where(p => p.UnallocatedAmount > 0).OrderBy(p => p.PaymentDate)) UnallocatedPayments.Add(p);

            HistorialCombinado.Clear();
            // Pasamos 'this' (PatientFinancialViewModel) en lugar del padre, porque ahora los comandos están aquí.
            // NOTA: Tendremos que actualizar CargoEvent y AbonoEvent para que acepten este VM.
            foreach (var c in VisitHistory) HistorialCombinado.Add(new CargoEvent(c, this));
            foreach (var p in PaymentHistory) HistorialCombinado.Add(new AbonoEvent(p, this));

            var sorted = HistorialCombinado.OrderByDescending(x => x.Timestamp).ToList();
            HistorialCombinado.Clear();
            sorted.ForEach(HistorialCombinado.Add);
        }

        private async Task OpenRegisterChargeDialog()
        {
            if (CurrentPatient == null) return;

            var currentUser = _authService.CurrentUser;
            if (currentUser == null) return;

            var (ok, data) = _dialogService.ShowManualChargeDialog(AvailableTreatments);
            if (ok && data != null)
            {
                decimal total = data.UnitPrice * data.Quantity;
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var repo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();
                        var entry = new ClinicalEntry
                        {
                            PatientId = CurrentPatient.Id,
                            DoctorId = currentUser.Id,
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
                    }
                    await RefreshBillingCollections();
                    _dialogService.ShowMessage("Cargo registrado.", "Éxito");
                }
                catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
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
                    await repo.AddAsync(new Payment
                    {
                        PatientId = CurrentPatient.Id,
                        PaymentDate = date ?? DateTime.Now,
                        Amount = amount,
                        Method = method,
                        Observaciones = obs
                    });
                    await repo.SaveChangesAsync();
                }
                await RefreshBillingCollections();
            }
            catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
        }

        private async Task AllocatePayment()
        {
            if (SelectedCharge == null || SelectedPayment == null || AmountToAllocate <= 0) return;
            if (AmountToAllocate > SelectedPayment.UnallocatedAmount || AmountToAllocate > SelectedCharge.Balance)
            {
                _dialogService.ShowMessage("Cantidad inválida.", "Error");
                return;
            }
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IRepository<PaymentAllocation>>();
                    await repo.AddAsync(new PaymentAllocation
                    {
                        PaymentId = SelectedPayment.Id,
                        ClinicalEntryId = SelectedCharge.Id,
                        AmountAllocated = AmountToAllocate
                    });
                    await repo.SaveChangesAsync();
                }
                await RefreshBillingCollections();
                SelectedCharge = null; SelectedPayment = null; AmountToAllocate = 0;
            }
            catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
        }

        private void AutoFillAmountToAllocate()
        {
            if (SelectedCharge != null && SelectedPayment != null)
                AmountToAllocate = Math.Min(SelectedCharge.Balance, SelectedPayment.UnallocatedAmount);
            else
                AmountToAllocate = 0;
        }

        private async Task DeleteClinicalEntryAsync(ClinicalEntry? e)
        {
            if (e == null || _dialogService.ShowConfirmation("¿Eliminar cargo?", "Confirmar") == CoreDialogResult.No) return;
            try
            {
                using (var s = _scopeFactory.CreateScope())
                {
                    await s.ServiceProvider.GetRequiredService<IClinicalEntryRepository>().DeleteEntryAndAllocationsAsync(e.Id);
                }
                await RefreshBillingCollections();
            }
            catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
        }

        private async Task DeletePaymentAsync(Payment? p)
        {
            if (p == null || _dialogService.ShowConfirmation("¿Eliminar este abono?\n\nSe anularán todas las asignaciones de pago correspondientes.", "Confirmar Eliminación de Pago") == CoreDialogResult.No) return;

            try
            {
                using (var s = _scopeFactory.CreateScope())
                {
                    var r = s.ServiceProvider.GetRequiredService<IPaymentRepository>();
                    var dbPayment = await r.GetByIdAsync(p.Id);
                    if (dbPayment != null)
                    {
                        r.Remove(dbPayment);
                        await r.SaveChangesAsync();
                    }
                    else
                    {
                        _dialogService.ShowMessage("No se encontró el pago en la base de datos.", "Error");
                    }
                }
                await RefreshBillingCollections();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al eliminar el pago: {ex.Message}", "Error");
            }
        }

        private void NewBudget()
        {
            if (CurrentPatient != null)
                WeakReferenceMessenger.Default.Send(new NavigateToNewBudgetMessage(CurrentPatient));
        }

        private async Task PrintHistoryAsync()
        {
            if (CurrentPatient != null)
            {
                try
                {
                    byte[] pdf;
                    using (var s = _scopeFactory.CreateScope())
                    {
                        // Nota: El servicio PDF necesita listas completas
                        pdf = await s.ServiceProvider.GetRequiredService<IPdfService>()
                            .GenerateHistoryPdfAsync(CurrentPatient, VisitHistory.ToList(), PaymentHistory.ToList(), CurrentBalance);
                    }
                    string p = Path.Combine(Path.GetTempPath(), $"Hist_{DateTime.Now.Ticks}.pdf");
                    await File.WriteAllBytesAsync(p, pdf);
                    Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
                }
                catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
            }
        }
    }
}