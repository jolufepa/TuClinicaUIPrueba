// En: TuClinicaUI/ViewModels/BudgetsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection; // Para IServiceProvider
using System;
using System.Collections.ObjectModel; // Para ObservableCollection
using System.ComponentModel;
using System.ComponentModel.DataAnnotations; // Para [Range]
using System.Diagnostics; // Para Process.Start (abrir PDF)
using System.IO; // Para Path
using System.Linq; // Para Sum, etc.
using System.Threading.Tasks;
using System.Windows; // Para MessageBox
using TuClinica.Core.Enums; // Para BudgetStatus
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
        [NotifyCanExecuteChangedFor(nameof(GenerateBudgetCommand))] // Habilitar generar si hay paciente e items
        [NotifyPropertyChangedFor(nameof(CurrentPatientDisplay))]
        private Patient? _currentPatient;

        [ObservableProperty]
        private ObservableCollection<Treatment> _availableTreatments = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Subtotal))] // Recalcular subtotal
        [NotifyPropertyChangedFor(nameof(DiscountAmount))] // Recalcular descuento
        [NotifyPropertyChangedFor(nameof(VatAmount))] // Recalcular IVA
        [NotifyPropertyChangedFor(nameof(TotalAmount))] // Recalcular total
        [NotifyCanExecuteChangedFor(nameof(GenerateBudgetCommand))] // Habilitar generar si hay paciente e items
        private ObservableCollection<BudgetLineItem> _budgetItems = new ObservableCollection<BudgetLineItem>();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DiscountAmount))]
        [NotifyPropertyChangedFor(nameof(VatAmount))]
        [NotifyPropertyChangedFor(nameof(TotalAmount))]
        private decimal _discountPercent = 0; // Por defecto 0%
        [ObservableProperty]
        private string _currentPatientDisplay = "Ningún paciente seleccionado";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(VatAmount))]
        [NotifyPropertyChangedFor(nameof(TotalAmount))]
        private decimal _vatPercent = 0; // Por defecto 0% IVA 

        // --- Propiedades de Financiación ---
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateBudgetCommand))]
        [NotifyPropertyChangedFor(nameof(MonthlyPayment))]
        [NotifyPropertyChangedFor(nameof(TotalFinanced))]
        [Range(0, 360)] // Rango de 0 a 30 años (360 meses)
        private int _numberOfMonths = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MonthlyPayment))]
        [NotifyPropertyChangedFor(nameof(TotalFinanced))]
        [Range(0, 100)] // Rango de 0% a 100%
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

        // --- Propiedades Calculadas (Formulario Creación) ---
        public decimal Subtotal => BudgetItems?.Sum(item => item.Quantity * item.UnitPrice) ?? 0;
        public decimal DiscountAmount => Subtotal * (DiscountPercent / 100);
        public decimal BaseImponible => Subtotal - DiscountAmount;
        public decimal VatAmount => BaseImponible * (VatPercent / 100);
        public decimal TotalAmount => BaseImponible + VatAmount;

        // --- Nuevas Propiedades Calculadas de Financiación ---
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

        public void Receive(NavigateToNewBudgetMessage message)
        {
        }

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

        partial void OnDiscountPercentChanged(decimal value)
        {
            RecalculateFinancing();
        }

        partial void OnVatPercentChanged(decimal value)
        {
            RecalculateFinancing();
        }

        partial void OnNumberOfMonthsChanged(int value)
        {
            RecalculateFinancing();
            OnPropertyChanged(nameof(IsFinanced));
        }

        partial void OnInterestRateChanged(decimal value)
        {
            RecalculateFinancing();
        }

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
                catch (Exception)
                {
                    MonthlyPayment = 0;
                    TotalFinanced = TotalAmount;
                }
            }

            OnPropertyChanged(nameof(MonthlyPayment));
            OnPropertyChanged(nameof(TotalFinanced));
        }


        // --- Comandos (Formulario Creación) ---

        private void SelectPatient()
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dialog = scope.ServiceProvider.GetRequiredService<PatientSelectionDialog>();

                    Window? ownerWindow = Application.Current.MainWindow;
                    if (ownerWindow != null && ownerWindow != dialog)
                    {
                        dialog.Owner = ownerWindow;
                    }

                    var result = dialog.ShowDialog();

                    var dialogViewModel = dialog.ViewModel;
                    if (dialogViewModel == null)
                    {
                        MessageBox.Show("Error interno: No se pudo obtener el ViewModel del diálogo de selección.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (result == true && dialogViewModel.SelectedPatientFromList != null)
                    {
                        CurrentPatient = dialogViewModel.SelectedPatientFromList;
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al abrir la selección de paciente:\n{ex.Message}", "Error");
            }
        }

        [RelayCommand]
        private void AddPredefinedTreatment(Treatment? selectedTreatment)
        {
            if (selectedTreatment != null)
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
        }

        [RelayCommand]
        private void AddManualTreatment()
        {
            var newItem = new BudgetLineItem
            {
                Description = "Tratamiento Manual",
                Quantity = 1,
                UnitPrice = 0
            };
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
            if (item is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += BudgetItem_PropertyChanged;
            }
        }

        private void UnhookPropertyChanged(BudgetLineItem item)
        {
            if (item is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged -= BudgetItem_PropertyChanged;
            }
        }

        private void BudgetItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BudgetLineItem.Quantity) || e.PropertyName == nameof(BudgetLineItem.UnitPrice))
            {
                RecalculateTotalsOnItemChange();
            }
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
                Patient = null,

                NumberOfMonths = this.NumberOfMonths,
                InterestRate = this.InterestRate
            };

            bool guardadoConExito = false;
            int intentosMaximos = 3;

            for (int i = 0; i < intentosMaximos; i++)
            {
                try
                {
                    newBudget.BudgetNumber = await _budgetRepository.GetNextBudgetNumberAsync();

                    await _budgetRepository.AddAsync(newBudget);
                    await _budgetRepository.SaveChangesAsync();

                    guardadoConExito = true;
                    break;
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException != null && ex.InnerException.Message.Contains("UNIQUE constraint failed: Budgets.BudgetNumber"))
                    {
                        await Task.Delay(50);
                    }
                    else
                    {
                        _dialogService.ShowMessage($"Error al guardar el presupuesto en la base de datos:\n{ex.Message}", "Error BD");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Error inesperado al guardar:\n{ex.Message}", "Error General");
                    break;
                }
            }

            if (!guardadoConExito)
            {
                return;
            }

            string pdfPath = string.Empty;
            try
            {
                pdfPath = await _pdfService.GenerateBudgetPdfAsync(newBudget);
                newBudget.PdfFilePath = pdfPath;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al generar el archivo PDF:\n{ex.Message}", "Error PDF");
                return;
            }

            try
            {
                _budgetRepository.Update(newBudget);
                await _budgetRepository.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al actualizar la ruta del PDF en la base de datos:\n{ex.Message}", "Error BD");
                return;
            }


            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(pdfPath) { UseShellExecute = true };
                Process.Start(psi);

                var printResult = _dialogService.ShowConfirmation("Presupuesto generado y guardado.\n¿Desea imprimirlo ahora?",
                                                  "Imprimir Presupuesto");
                if (printResult == CoreDialogResult.Yes)
                {
                    _dialogService.ShowMessage("Por favor, use la opción de imprimir de su visor de PDF.", "Imprimir");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"No se pudo abrir el archivo PDF:\n{ex.Message}", "Error Abrir PDF");
            }

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
                foreach (var budget in history)
                {
                    BudgetHistory.Add(budget);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al cargar el historial de presupuestos:\n{ex.Message}", "Error");
            }
        }

        [RelayCommand(CanExecute = nameof(CanOpenPdf))]
        private void OpenPdf(Budget? budgetToOpen)
        {
            var budget = budgetToOpen ?? SelectedBudgetFromHistory;

            if (budget == null) return;

            if (string.IsNullOrEmpty(budget.PdfFilePath) || !File.Exists(budget.PdfFilePath))
            {
                _dialogService.ShowMessage($"No se encontró el archivo PDF para este presupuesto.\n{budget.PdfFilePath}", "Archivo no encontrado");
                return;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(budget.PdfFilePath) { UseShellExecute = true };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"No se pudo abrir el archivo PDF:\n{ex.Message}", "Error");
            }
        }

        private bool CanOpenPdf(Budget? budget)
        {
            var budgetToCheck = budget ?? SelectedBudgetFromHistory;
            return budgetToCheck != null && !string.IsNullOrEmpty(budgetToCheck.PdfFilePath) && File.Exists(budgetToCheck.PdfFilePath);
        }

        private bool CanUpdateStatus()
        {
            return SelectedBudgetFromHistory != null;
        }

        [RelayCommand(CanExecute = nameof(CanUpdateStatus))]
        private async Task MarkAsAcceptedAsync()
        {
            await UpdateSelectedBudgetStatusAsync(BudgetStatus.Aceptado);
        }

        [RelayCommand(CanExecute = nameof(CanUpdateStatus))]
        private async Task MarkAsRejectedAsync()
        {
            await UpdateSelectedBudgetStatusAsync(BudgetStatus.Rechazado);
        }

        [RelayCommand(CanExecute = nameof(CanUpdateStatus))]
        private async Task MarkAsPendingAsync()
        {
            await UpdateSelectedBudgetStatusAsync(BudgetStatus.Pendiente);
        }

        private async Task UpdateSelectedBudgetStatusAsync(BudgetStatus newStatus)
        {
            if (SelectedBudgetFromHistory == null)
            {
                _dialogService.ShowMessage("Por favor, selecciona un presupuesto del historial.", "Selección requerida");
                return;
            }

            var result = _dialogService.ShowConfirmation($"¿Estás seguro de que quieres marcar este presupuesto como '{newStatus}'?",
                                             "Confirmar cambio de estado");

            if (result == CoreDialogResult.No) return;

            var budgetToUpdate = SelectedBudgetFromHistory;

            try
            {
                await _budgetRepository.UpdateStatusAsync(budgetToUpdate.Id, newStatus);

                await LoadBudgetHistoryAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al actualizar el estado del presupuesto:\n{ex.Message}", "Error de base de datos");
            }
        }
    }
}