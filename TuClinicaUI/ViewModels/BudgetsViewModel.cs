using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic; // Necesario para List
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.UI.Messages;
using TuClinica.UI.Services;
using TuClinica.UI.Views;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;

namespace TuClinica.UI.ViewModels
{
    public partial class BudgetsViewModel : BaseViewModel, IRecipient<NavigateToNewBudgetMessage>
    {
        // --- Servicios Inyectados ---
        private readonly IPatientRepository _patientRepository;
        private readonly ITreatmentRepository _treatmentRepository;
        private readonly IBudgetRepository _budgetRepository;
        private readonly IPdfService _pdfService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDialogService _dialogService;

        public IRelayCommand SelectPatientCommand { get; }

        // --- Estado del ViewModel (Formulario de Creación) ---
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPatientSelected))]
        [NotifyCanExecuteChangedFor(nameof(GenerateBudgetCommand))]
        [NotifyPropertyChangedFor(nameof(CurrentPatientDisplay))]
        private Patient? _currentPatient;

        [ObservableProperty]
        private ObservableCollection<Treatment> _availableTreatments = new();

        // --- NUEVA PROPIEDAD PARA AUTO-AÑADIR ---
        // Esta propiedad se enlaza al ComboBox. Al seleccionar algo (set), lo añade y se resetea.
        private Treatment? _selectedPredefinedTreatment;
        public Treatment? SelectedPredefinedTreatment
        {
            get => _selectedPredefinedTreatment;
            set
            {
                if (SetProperty(ref _selectedPredefinedTreatment, value))
                {
                    if (value != null)
                    {
                        // 1. Ejecutar la lógica de añadir
                        AddPredefinedTreatment(value);

                        // 2. Resetear la selección para permitir seleccionar el mismo u otro inmediatamente
                        // Usamos el dispatcher para evitar conflictos de reentrancia en la UI
                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            SetProperty(ref _selectedPredefinedTreatment, null);
                            OnPropertyChanged(nameof(SelectedPredefinedTreatment));
                        });
                    }
                }
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Subtotal))]
        [NotifyPropertyChangedFor(nameof(DiscountAmount))]
        [NotifyPropertyChangedFor(nameof(VatAmount))]
        [NotifyPropertyChangedFor(nameof(TotalAmount))]
        [NotifyCanExecuteChangedFor(nameof(GenerateBudgetCommand))]
        private ObservableCollection<BudgetLineItem> _budgetItems = new ObservableCollection<BudgetLineItem>();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DiscountAmount))]
        [NotifyPropertyChangedFor(nameof(VatAmount))]
        [NotifyPropertyChangedFor(nameof(TotalAmount))]
        private decimal _discountPercent = 0;

        [ObservableProperty]
        private string _currentPatientDisplay = "Ningún paciente seleccionado";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(VatAmount))]
        [NotifyPropertyChangedFor(nameof(TotalAmount))]
        private decimal _vatPercent = 0;

        // --- Propiedades de Financiación ---
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateBudgetCommand))]
        [NotifyPropertyChangedFor(nameof(MonthlyPayment))]
        [NotifyPropertyChangedFor(nameof(TotalFinanced))]
        [Range(0, 360)]
        private int _numberOfMonths = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MonthlyPayment))]
        [NotifyPropertyChangedFor(nameof(TotalFinanced))]
        [Range(0, 100)]
        private decimal _interestRate = 0;

        // --- Propiedades para el HISTORIAL ---
        [ObservableProperty]
        private ObservableCollection<Budget> _budgetHistory = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(OpenPdfCommand))]
        [NotifyCanExecuteChangedFor(nameof(MarkAsAcceptedCommand))]
        [NotifyCanExecuteChangedFor(nameof(MarkAsRejectedCommand))]
        [NotifyCanExecuteChangedFor(nameof(MarkAsPendingCommand))]
        private Budget? _selectedBudgetFromHistory;

        // --- Propiedades Calculadas ---
        public decimal Subtotal => BudgetItems?.Sum(item => item.Quantity * item.UnitPrice) ?? 0;
        public decimal DiscountAmount => Subtotal * (DiscountPercent / 100);
        public decimal BaseImponible => Subtotal - DiscountAmount;
        public decimal VatAmount => BaseImponible * (VatPercent / 100);
        public decimal TotalAmount => BaseImponible + VatAmount;

        public decimal MonthlyPayment { get; private set; }
        public decimal TotalFinanced { get; private set; }
        public bool IsFinanced => NumberOfMonths > 0;

        public bool IsPatientSelected => CurrentPatient != null;
        public bool CanGenerateBudget => IsPatientSelected && BudgetItems.Any();

        public BudgetsViewModel(
            IPatientRepository patientRepository,
            ITreatmentRepository treatmentRepository,
            IBudgetRepository budgetRepository,
            IPdfService pdfService,
            IServiceScopeFactory scopeFactory,
            IDialogService dialogService)
        {
            _patientRepository = patientRepository;
            _treatmentRepository = treatmentRepository;
            _budgetRepository = budgetRepository;
            _pdfService = pdfService;
            _scopeFactory = scopeFactory;
            _dialogService = dialogService;

            SelectPatientCommand = new RelayCommand(SelectPatient);

            BudgetItems.CollectionChanged += (s, e) => RecalculateTotalsOnItemChange();

            _ = LoadAvailableTreatmentsAsync();
            _ = LoadBudgetHistoryAsync();

            WeakReferenceMessenger.Default.Register(this);
        }

        public void Receive(NavigateToNewBudgetMessage message) { }

        public void SetPatientForNewBudget(Patient patient)
        {
            ClearBudget();
            CurrentPatient = patient;
        }

        partial void OnCurrentPatientChanged(Patient? value)
        {
            CurrentPatientDisplay = value?.PatientDisplayInfo ?? "Ningún paciente seleccionado";
        }

        private async Task LoadAvailableTreatmentsAsync()
        {
            var treatments = await _treatmentRepository.GetAllAsync();
            AvailableTreatments.Clear();
            if (treatments != null)
            {
                foreach (var treatment in treatments.Where(t => t.IsActive))
                {
                    AvailableTreatments.Add(treatment);
                }
            }
        }

        private void RecalculateTotalsOnItemChange()
        {
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(DiscountAmount));
            OnPropertyChanged(nameof(BaseImponible));
            OnPropertyChanged(nameof(VatAmount));
            OnPropertyChanged(nameof(TotalAmount));
            RecalculateFinancing();
            GenerateBudgetCommand.NotifyCanExecuteChanged();
        }

        partial void OnDiscountPercentChanged(decimal value) => RecalculateFinancing();
        partial void OnVatPercentChanged(decimal value) => RecalculateFinancing();
        partial void OnNumberOfMonthsChanged(int value) { RecalculateFinancing(); OnPropertyChanged(nameof(IsFinanced)); }
        partial void OnInterestRateChanged(decimal value) => RecalculateFinancing();

        private void RecalculateFinancing()
        {
            if (NumberOfMonths <= 0 || TotalAmount == 0)
            {
                MonthlyPayment = 0;
                TotalFinanced = TotalAmount;
            }
            else if (InterestRate == 0)
            {
                MonthlyPayment = TotalAmount / NumberOfMonths;
                TotalFinanced = TotalAmount;
            }
            else
            {
                try
                {
                    double totalV = (double)TotalAmount;
                    double i = (double)(InterestRate / 100) / 12;
                    int n = NumberOfMonths;
                    double monthlyPaymentDouble = (totalV * i) / (1 - Math.Pow(1 + i, -n));
                    MonthlyPayment = (decimal)Math.Round(monthlyPaymentDouble, 2, MidpointRounding.AwayFromZero);
                    TotalFinanced = MonthlyPayment * n;
                }
                catch { MonthlyPayment = 0; TotalFinanced = TotalAmount; }
            }
            OnPropertyChanged(nameof(MonthlyPayment));
            OnPropertyChanged(nameof(TotalFinanced));
        }

        // --- Comandos ---

        private void SelectPatient()
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dialog = scope.ServiceProvider.GetRequiredService<PatientSelectionDialog>();
                    Window? ownerWindow = Application.Current.MainWindow;
                    if (ownerWindow != null && ownerWindow != dialog) dialog.Owner = ownerWindow;

                    var result = dialog.ShowDialog();
                    if (result == true && dialog.ViewModel.SelectedPatientFromList != null)
                    {
                        CurrentPatient = dialog.ViewModel.SelectedPatientFromList;
                    }
                }
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); }
        }

        [RelayCommand]
        private void AddPredefinedTreatment(Treatment? selectedTreatment)
        {
            if (selectedTreatment == null) return;

            if (selectedTreatment.PackItems != null && selectedTreatment.PackItems.Any())
            {
                foreach (var packItem in selectedTreatment.PackItems)
                {
                    if (packItem.ChildTreatment != null)
                    {
                        var childItem = new BudgetLineItem
                        {
                            Description = packItem.ChildTreatment.Name,
                            Quantity = packItem.Quantity,
                            UnitPrice = packItem.ChildTreatment.DefaultPrice
                        };
                        BudgetItems.Add(childItem);
                        HookPropertyChanged(childItem);
                    }
                }
            }
            else
            {
                var newItem = new BudgetLineItem
                {
                    Description = selectedTreatment.Name,
                    Quantity = 1,
                    UnitPrice = selectedTreatment.DefaultPrice
                };
                BudgetItems.Add(newItem);
                HookPropertyChanged(newItem);
            }
            RecalculateTotalsOnItemChange();
        }

        [RelayCommand]
        private void AddManualTreatment()
        {
            var newItem = new BudgetLineItem { Description = "Tratamiento Manual", Quantity = 1, UnitPrice = 0 };
            BudgetItems.Add(newItem);
            HookPropertyChanged(newItem);
        }

        [RelayCommand]
        private void RemoveBudgetItem(BudgetLineItem? itemToRemove)
        {
            if (itemToRemove != null)
            {
                UnhookPropertyChanged(itemToRemove);
                BudgetItems.Remove(itemToRemove);
            }
        }

        private void HookPropertyChanged(BudgetLineItem item)
        {
            if (item is INotifyPropertyChanged npc) npc.PropertyChanged += BudgetItem_PropertyChanged;
        }
        private void UnhookPropertyChanged(BudgetLineItem item)
        {
            if (item is INotifyPropertyChanged npc) npc.PropertyChanged -= BudgetItem_PropertyChanged;
        }
        private void BudgetItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BudgetLineItem.Quantity) || e.PropertyName == nameof(BudgetLineItem.UnitPrice))
                RecalculateTotalsOnItemChange();
        }

        [RelayCommand(CanExecute = nameof(CanGenerateBudget))]
        private async Task GenerateBudgetAsync()
        {
            if (CurrentPatient == null) return;

            var newBudget = new Budget
            {
                PatientId = CurrentPatient.Id,
                IssueDate = DateTime.Now,
                BudgetNumber = await _budgetRepository.GetNextBudgetNumberAsync(),
                Items = new List<BudgetLineItem>(BudgetItems.Select(bi => new BudgetLineItem
                {
                    Description = bi.Description,
                    Quantity = bi.Quantity,
                    UnitPrice = bi.UnitPrice,
                })),
                Subtotal = Subtotal,
                DiscountPercent = DiscountPercent,
                VatPercent = VatPercent,
                TotalAmount = TotalAmount,
                Status = BudgetStatus.Pendiente,
                NumberOfMonths = this.NumberOfMonths,
                InterestRate = this.InterestRate
            };

            bool guardadoConExito = false;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    newBudget.BudgetNumber = await _budgetRepository.GetNextBudgetNumberAsync();
                    await _budgetRepository.AddAsync(newBudget);
                    await _budgetRepository.SaveChangesAsync();
                    guardadoConExito = true;
                    break;
                }
                catch (DbUpdateException) { await Task.Delay(50); }
                catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); break; }
            }

            if (!guardadoConExito) return;

            try
            {
                string pdfPath = await _pdfService.GenerateBudgetPdfAsync(newBudget);
                newBudget.PdfFilePath = pdfPath;
                _budgetRepository.Update(newBudget);
                await _budgetRepository.SaveChangesAsync();

                Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error PDF: {ex.Message}", "Error"); }

            ClearBudget();
            await LoadBudgetHistoryAsync();
        }

        [RelayCommand]
        private void ClearBudget()
        {
            CurrentPatient = null;
            BudgetItems.Clear();
            DiscountPercent = 0;
            VatPercent = 0;
            NumberOfMonths = 0;
            InterestRate = 0;
        }

        [RelayCommand]
        private async Task LoadBudgetHistoryAsync()
        {
            try
            {
                var history = await _budgetRepository.FindBudgetsAsync();
                BudgetHistory.Clear();
                foreach (var budget in history) BudgetHistory.Add(budget);
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error Historial: {ex.Message}", "Error"); }
        }

        [RelayCommand(CanExecute = nameof(CanOpenPdf))]
        private void OpenPdf(Budget? budgetToOpen)
        {
            var budget = budgetToOpen ?? SelectedBudgetFromHistory;
            if (budget != null && !string.IsNullOrEmpty(budget.PdfFilePath) && File.Exists(budget.PdfFilePath))
            {
                Process.Start(new ProcessStartInfo(budget.PdfFilePath) { UseShellExecute = true });
            }
            else
            {
                _dialogService.ShowMessage("Archivo no encontrado.", "Error");
            }
        }

        private bool CanOpenPdf(Budget? budget) => (budget ?? SelectedBudgetFromHistory) != null;
        private bool CanUpdateStatus() => SelectedBudgetFromHistory != null;

        [RelayCommand(CanExecute = nameof(CanUpdateStatus))]
        private async Task MarkAsAcceptedAsync() => await UpdateSelectedBudgetStatusAsync(BudgetStatus.Aceptado);
        [RelayCommand(CanExecute = nameof(CanUpdateStatus))]
        private async Task MarkAsRejectedAsync() => await UpdateSelectedBudgetStatusAsync(BudgetStatus.Rechazado);
        [RelayCommand(CanExecute = nameof(CanUpdateStatus))]
        private async Task MarkAsPendingAsync() => await UpdateSelectedBudgetStatusAsync(BudgetStatus.Pendiente);

        private async Task UpdateSelectedBudgetStatusAsync(BudgetStatus newStatus)
        {
            if (SelectedBudgetFromHistory == null) return;
            if (_dialogService.ShowConfirmation($"¿Marcar como '{newStatus}'?", "Confirmar") == CoreDialogResult.Yes)
            {
                try
                {
                    await _budgetRepository.UpdateStatusAsync(SelectedBudgetFromHistory.Id, newStatus);
                    await LoadBudgetHistoryAsync();
                }
                catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
            }
        }
    }
}