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
        private readonly IServiceProvider _serviceProvider;
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


        // --- Constructor ---
        public BudgetsViewModel(
            IPatientRepository patientRepository,
            ITreatmentRepository treatmentRepository,
            IBudgetRepository budgetRepository,
            IPdfService pdfService,
            IServiceProvider serviceProvider,
            IDialogService dialogService)
        {
            _patientRepository = patientRepository;
            _treatmentRepository = treatmentRepository;
            _budgetRepository = budgetRepository;
            _pdfService = pdfService;
            _serviceProvider = serviceProvider;
            _dialogService = dialogService;

            SelectPatientCommand = new RelayCommand(SelectPatient);

            // Escuchar cambios en la colección de items para recalcular
            BudgetItems.CollectionChanged += (s, e) => RecalculateTotalsOnItemChange();

            // Cargar tratamientos disponibles al inicio
            _ = LoadAvailableTreatmentsAsync();

            // Cargar historial al iniciar
            _ = LoadBudgetHistoryAsync();

            WeakReferenceMessenger.Default.Register(this);
        }

        /// <summary>
        /// Recibe el mensaje para pre-seleccionar un paciente.
        /// </summary>
        public void Receive(NavigateToNewBudgetMessage message)
        {
            // Este método es llamado por MainWindowViewModel
            // (Esta lógica la movimos en el paso anterior)
        }

        /// <summary>
        /// Configura el ViewModel para un nuevo presupuesto, pre-seleccionando un paciente.
        /// </summary>
        public void SetPatientForNewBudget(Patient patient)
        {
            ClearBudget();
            CurrentPatient = patient;
        }


        // Este método se llama automáticamente cuando CurrentPatient cambia
        partial void OnCurrentPatientChanged(Patient? value)
        {
            // Actualiza la propiedad de display, usando la lógica del modelo Patient
            CurrentPatientDisplay = value?.PatientDisplayInfo ?? "Ningún paciente seleccionado";
        }
        // Método para cargar tratamientos activos
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

        // Método llamado cuando se añade/quita un item o cambian sus propiedades internas
        private void RecalculateTotalsOnItemChange()
        {
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(DiscountAmount));
            OnPropertyChanged(nameof(BaseImponible));
            OnPropertyChanged(nameof(VatAmount));
            OnPropertyChanged(nameof(TotalAmount));
            RecalculateFinancing(); // <-- AÑADIDO
            GenerateBudgetCommand.NotifyCanExecuteChanged();
        }

        // --- CORRECCIÓN: Métodos parciales para reaccionar a cambios ---

        // El método erróneo OnTotalAmountChanged() ha sido eliminado.

        // Estos métodos SÍ existen porque DiscountPercent y VatPercent son [ObservableProperty]
        partial void OnDiscountPercentChanged(decimal value)
        {
            // Esta propiedad también afecta a TotalAmount, así que recalculamos
            RecalculateFinancing();
        }

        partial void OnVatPercentChanged(decimal value)
        {
            // Esta propiedad también afecta a TotalAmount, así que recalculamos
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
        // --- FIN DE LA CORRECCIÓN ---


        // --- Lógica de cálculo de financiación ---
        private void RecalculateFinancing()
        {
            if (NumberOfMonths <= 0 || TotalAmount == 0)
            {
                MonthlyPayment = 0;
                TotalFinanced = TotalAmount; // El total es el pago único
            }
            else if (InterestRate == 0)
            {
                // Financiación simple 0%
                MonthlyPayment = TotalAmount / NumberOfMonths;
                TotalFinanced = TotalAmount;
            }
            else
            {
                // Cálculo profesional (Fórmula de amortización de préstamo - Sistema Francés)
                try
                {
                    // Convertimos a double para usar Math.Pow
                    double totalV = (double)TotalAmount;
                    // Interés mensual (ej: 5.5% anual -> 0.055 / 12)
                    double i = (double)(InterestRate / 100) / 12;
                    int n = NumberOfMonths;

                    // Fórmula: Cuota = [V * i] / [1 - (1 + i)^-n]
                    double monthlyPaymentDouble = (totalV * i) / (1 - Math.Pow(1 + i, -n));

                    // Redondeamos a 2 decimales
                    MonthlyPayment = (decimal)Math.Round(monthlyPaymentDouble, 2, MidpointRounding.AwayFromZero);
                    TotalFinanced = MonthlyPayment * n;
                }
                catch (Exception)
                {
                    // Evita errores si los valores son extremos (ej. Overflow)
                    MonthlyPayment = 0;
                    TotalFinanced = TotalAmount;
                }
            }

            // Notificamos a la UI
            OnPropertyChanged(nameof(MonthlyPayment));
            OnPropertyChanged(nameof(TotalFinanced));
        }


        // --- Comandos (Formulario Creación) ---

        // Método que ejecuta el comando SelectPatientCommand (Corregido)
        private void SelectPatient()
        {
            try
            {
                var dialog = _serviceProvider.GetRequiredService<PatientSelectionDialog>();

                // --- ASIGNACIÓN DE PROPIETARIO MÁS SEGURA ---
                Window? ownerWindow = Application.Current.MainWindow;
                // Comprobar si MainWindow existe y NO es el propio diálogo
                if (ownerWindow != null && ownerWindow != dialog)
                {
                    dialog.Owner = ownerWindow;
                }
                else
                {
                    // Si no se puede asignar (ej. MainWindow aún no lista), el diálogo se abrirá centrado en la pantalla.
                }
                // --- FIN ASIGNACIÓN SEGURA ---

                var result = dialog.ShowDialog(); // Muestra el diálogo modalmente

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
                else
                {
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al abrir la selección de paciente:\n{ex.Message}", "Error");
            }
        }// Fin del método SelectPatient

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

        // Método para escuchar cambios en Quantity/UnitPrice de un item
        private void HookPropertyChanged(BudgetLineItem item)
        {
            if (item is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += BudgetItem_PropertyChanged;
            }
        }

        // Método para dejar de escuchar
        private void UnhookPropertyChanged(BudgetLineItem item)
        {
            if (item is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged -= BudgetItem_PropertyChanged;
            }
        }

        // Handler que recalcula totales cuando cambia Quantity o UnitPrice
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

            // --- 1. Crear el objeto Budget ---
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

                // --- AÑADIR ESTAS LÍNEAS ---
                NumberOfMonths = this.NumberOfMonths,
                InterestRate = this.InterestRate
                // --- FIN LÍNEAS AÑADIDAS ---
            };

            // --- 2. Guardar en BD (con manejo de reintentos) ---
            bool guardadoConExito = false;
            int intentosMaximos = 3; // Para evitar un bucle infinito

            for (int i = 0; i < intentosMaximos; i++)
            {
                try
                {
                    // Obtenemos el número DENTRO del bucle
                    newBudget.BudgetNumber = await _budgetRepository.GetNextBudgetNumberAsync();

                    await _budgetRepository.AddAsync(newBudget);
                    await _budgetRepository.SaveChangesAsync();

                    guardadoConExito = true; // Si llega aquí, se guardó
                    break; // Salimos del bucle
                }
                catch (DbUpdateException ex)
                {
                    // Comprobamos si el error es por la restricción UNIQUE
                    if (ex.InnerException != null && ex.InnerException.Message.Contains("UNIQUE constraint failed: Budgets.BudgetNumber"))
                    {
                        // Es una colisión (race condition).
                        // El bucle continuará y volverá a intentarlo
                        // con un nuevo número en la siguiente iteración.
                        await Task.Delay(50); // Pequeña espera opcional
                    }
                    else
                    {
                        // Fue otro error de BD
                        _dialogService.ShowMessage($"Error al guardar el presupuesto en la base de datos:\n{ex.Message}", "Error BD");
                        break; // Salir del bucle, no reintentar
                    }
                }
                catch (Exception ex)
                {
                    // Fue un error general
                    _dialogService.ShowMessage($"Error inesperado al guardar:\n{ex.Message}", "Error General");
                    break; // Salir del bucle
                }
            }

            // Si después de los reintentos no se pudo guardar, informamos y salimos.
            if (!guardadoConExito)
            {
                return;
            }

            // --- 3. Generar PDF ---
            string pdfPath = string.Empty;
            try
            {


                pdfPath = await _pdfService.GenerateBudgetPdfAsync(newBudget);
                newBudget.PdfFilePath = pdfPath; // Guardamos la ruta
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al generar el archivo PDF:\n{ex.Message}", "Error PDF");
                return;
            }

            // --- 4. Actualizar BD con la ruta del PDF ---
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


            // --- 5. Mostrar PDF y Preguntar Imprimir (Tu requisito) ---
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

            // --- 6. Limpiar formulario ---
            ClearBudget();

            // Refrescar el historial
            await LoadBudgetHistoryAsync();
        }

        [RelayCommand]
        private void ClearBudget()
        {
            CurrentPatient = null;
            BudgetItems.Clear();
            DiscountPercent = 0;
            VatPercent = 0; // Restablecer a 0
            NumberOfMonths = 0; // <-- AÑADIDO
            InterestRate = 0;   // <-- AÑADIDO
        }

        // --- Métodos y Comandos para el HISTORIAL ---

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

        // Propiedad para habilitar el botón "Abrir PDF"
        private bool CanOpenPdf(Budget? budget)
        {
            var budgetToCheck = budget ?? SelectedBudgetFromHistory;
            return budgetToCheck != null && !string.IsNullOrEmpty(budgetToCheck.PdfFilePath) && File.Exists(budgetToCheck.PdfFilePath);
        }


        // *** NUEVO: Comandos para actualizar el estado del presupuesto ***

        // Helper CanExecute para los comandos de estado
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
            // Solo si queremos permitir volver a "Pendiente" un presupuesto
            await UpdateSelectedBudgetStatusAsync(BudgetStatus.Pendiente);
        }

        // Método principal que actualiza la BD y la UI
        private async Task UpdateSelectedBudgetStatusAsync(BudgetStatus newStatus)
        {
            if (SelectedBudgetFromHistory == null)
            {
                _dialogService.ShowMessage("Por favor, selecciona un presupuesto del historial.", "Selección requerida");
                return;
            }

            // Opcional: Confirmación
            var result = _dialogService.ShowConfirmation($"¿Estás seguro de que quieres marcar este presupuesto como '{newStatus}'?",
                                             "Confirmar cambio de estado");

            if (result == CoreDialogResult.No) return;

            var budgetToUpdate = SelectedBudgetFromHistory;

            try
            {
                // 1. Actualizar la base de datos
                await _budgetRepository.UpdateStatusAsync(budgetToUpdate.Id, newStatus);

                // 2. Recargar el historial para reflejar el cambio en la UI
                // Esta es la forma más robusta de asegurar que la UI está sincronizada.
                await LoadBudgetHistoryAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al actualizar el estado del presupuesto:\n{ex.Message}", "Error de base de datos");
            }
        }
    }
}