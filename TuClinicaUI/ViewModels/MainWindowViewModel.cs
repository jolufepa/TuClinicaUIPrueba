using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // <-- Este SÍ se queda
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Windows;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.UI.Messages; // <-- Este SÍ se queda

// 1. ASEGÚRATE DE QUE EL NAMESPACE ES ESTE
namespace TuClinica.UI.ViewModels
{
    // 2. IRecipient<...> SE QUEDA
    public partial class MainWindowViewModel : BaseViewModel, IRecipient<NavigateToNewBudgetMessage>
    {
        private readonly IAuthService _authService;
        private readonly IServiceProvider _serviceProvider;
        private User? _currentUser;

        // 1. Guardamos una referencia al VM singleton
        private readonly PatientFileViewModel _patientFileViewModel;
        private readonly HomeViewModel _homeViewModel;

        [ObservableProperty]
        private bool _isAdminMenuVisible;
        [ObservableProperty]
        private bool _isDoctorMenuVisible;
        [ObservableProperty]
        private bool _isReceptionMenuVisible;

        // *** AÑADIDO: Visibilidad para Recetas ***
        [ObservableProperty]
        private bool _isPrescriptionMenuVisible;

        [ObservableProperty]
        private BaseViewModel? _selectedViewModel;

        // 2. Modifica el constructor
        public MainWindowViewModel(IAuthService authService,
                                   IServiceProvider serviceProvider,
                                   PatientFileViewModel patientFileViewModel,
                                   HomeViewModel homeViewModel)
        {
            _authService = authService;
            _serviceProvider = serviceProvider;
            _patientFileViewModel = patientFileViewModel;

            _homeViewModel = homeViewModel;
            LoadCurrentUserAndSetVisibility();

            try
            {
                // 3. Modificamos cómo se crea PatientsViewModel
                var patientsVM = _serviceProvider.GetRequiredService<PatientsViewModel>();

                // 4. Le pasamos el comando de navegación al PatientsViewModel
                patientsVM.SetNavigationCommand(NavigateToPatientFileCommand);

                SelectedViewModel = _homeViewModel;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error crítico al cargar la vista inicial:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            _homeViewModel = homeViewModel;

            // 3. REGISTRARSE A LOS MENSAJES
            WeakReferenceMessenger.Default.Register(this);
        }

        // 4. AÑADIR EL MÉTODO Receive (¡MODIFICADO!)
        /// <summary>
        /// Recibe el mensaje para navegar a la vista de presupuestos.
        /// </summary>
        public void Receive(NavigateToNewBudgetMessage message)
        {
            // --- INICIO DE LA LÓGICA CORREGIDA ---

            // 1. Obtenemos una NUEVA instancia de BudgetsViewModel
            var budgetVM = _serviceProvider.GetRequiredService<BudgetsViewModel>();

            // 2. Llamamos al método público para pre-configurarla
            budgetVM.SetPatientForNewBudget(message.Value);

            // 3. Establecemos esta instancia pre-configurada como la vista activa
            SelectedViewModel = budgetVM;

            // --- FIN DE LA LÓGICA CORREGIDA ---
        }

        private void LoadCurrentUserAndSetVisibility()
        {
            _currentUser = _authService.CurrentUser;

            // Ocultar todo por defecto
            IsAdminMenuVisible = false;
            IsDoctorMenuVisible = false;
            IsReceptionMenuVisible = false;
            IsPrescriptionMenuVisible = false; // *** AÑADIDO ***

            if (_currentUser != null)
            {
                // Roles básicos (Recepción, Doctor, Admin)
                if (_currentUser.Role == UserRole.Recepcionista || _currentUser.Role == UserRole.Doctor || _currentUser.Role == UserRole.Administrador)
                {
                    IsReceptionMenuVisible = true; // Pacientes y Presupuestos
                }

                // Roles clínicos (Doctor, Admin)
                if (_currentUser.Role == UserRole.Doctor || _currentUser.Role == UserRole.Administrador)
                {
                    IsDoctorMenuVisible = true; // Tratamientos
                    IsPrescriptionMenuVisible = true; // *** AÑADIDO: Recetas ***
                }

                // Rol Admin
                if (_currentUser.Role == UserRole.Administrador)
                {
                    IsAdminMenuVisible = true; // Administración
                }
            }
        }

        // --- Comandos de Navegación ---

        [RelayCommand]
        private void NavigateToPatients()
        {
            // Modificamos este comando para que también configure la navegación
            var patientsVM = _serviceProvider.GetRequiredService<PatientsViewModel>();
            patientsVM.SetNavigationCommand(NavigateToPatientFileCommand);
            SelectedViewModel = patientsVM;
        }

        [RelayCommand]
        private void NavigateToBudgets()
        {
            // Esta navegación (desde el menú principal) debe mostrar un
            // ViewModel limpio, por eso pedimos uno nuevo.
            SelectedViewModel = _serviceProvider.GetRequiredService<BudgetsViewModel>();
        }

        [RelayCommand]
        private void NavigateToTreatments()
        {
            SelectedViewModel = _serviceProvider.GetRequiredService<TreatmentsViewModel>();
        }



        // *** AÑADIDO: Comando para Recetas ***
        [RelayCommand]
        private void NavigateToPrescriptions()
        {
            // ¡Módulo 'Recetas' ahora conectado!
            SelectedViewModel = _serviceProvider.GetRequiredService<PrescriptionViewModel>();
        }
        // *** FIN AÑADIDO ***** FIN AÑADIDO ***

        [RelayCommand]
        private void NavigateToAdmin()
        {
            SelectedViewModel = _serviceProvider.GetRequiredService<AdminViewModel>();
        }

        // 5. AÑADIMOS EL NUEVO COMANDO DE NAVEGACIÓN
        [RelayCommand]
        private void NavigateToPatientFile()
        {
            // Simplemente cambia la vista actual al singleton de Ficha de Paciente
            // El paciente correcto ya habrá sido cargado por PatientsViewModel
            SelectedViewModel = _patientFileViewModel;
        }
    }
}