// En: TuClinicaUI/ViewModels/MainWindowViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Windows;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.UI.Messages;

namespace TuClinica.UI.ViewModels
{
    public partial class MainWindowViewModel : BaseViewModel, IRecipient<NavigateToNewBudgetMessage>
    {
        private readonly IAuthService _authService;
        private readonly IServiceScopeFactory _scopeFactory;
        private User? _currentUser;

        // --- INICIO DE LA CORRECCIÓN ---
        // Este campo mantendrá el ámbito (y sus servicios Scoped como AppDbContext)
        // vivo mientras la vista esté activa.
        private IServiceScope? _currentViewScope;
        // --- FIN DE LA CORRECCIÓN ---

        private readonly PatientFileViewModel _patientFileViewModel;
        private readonly HomeViewModel _homeViewModel;

        [ObservableProperty]
        private bool _isAdminMenuVisible;
        [ObservableProperty]
        private bool _isDoctorMenuVisible;
        [ObservableProperty]
        private bool _isReceptionMenuVisible;
        [ObservableProperty]
        private bool _isPrescriptionMenuVisible;

        [ObservableProperty]
        private BaseViewModel? _selectedViewModel;

        public MainWindowViewModel(IAuthService authService,
                                   IServiceScopeFactory scopeFactory,
                                   PatientFileViewModel patientFileViewModel,
                                   HomeViewModel homeViewModel)
        {
            _authService = authService;
            _scopeFactory = scopeFactory;
            _patientFileViewModel = patientFileViewModel;
            _homeViewModel = homeViewModel;

            LoadCurrentUserAndSetVisibility();

            // --- INICIO DE LA CORRECCIÓN ---
            // El bloque try-catch original aquí no era necesario,
            // ya que el 'patientsVM' que creaba era temporal y se descartaba,
            // no afectaba a la navegación real.
            // La navegación inicial es a HomeViewModel (un Singleton).
            SelectedViewModel = _homeViewModel;
            // --- FIN DE LA CORRECCIÓN ---

            WeakReferenceMessenger.Default.Register(this);
        }

        public void Receive(NavigateToNewBudgetMessage message)
        {
            // --- CORRECCIÓN: Gestionar el ciclo de vida del ámbito ---

            // 1. Destruir el ámbito anterior (si existe)
            _currentViewScope?.Dispose();

            // 2. Crear un nuevo ámbito
            _currentViewScope = _scopeFactory.CreateScope();

            // 3. Resolver la vista desde el NUEVO ámbito
            var budgetVM = _currentViewScope.ServiceProvider.GetRequiredService<BudgetsViewModel>();

            // 4. Configurar y mostrar
            budgetVM.SetPatientForNewBudget(message.Value);
            SelectedViewModel = budgetVM;
        }

        private void LoadCurrentUserAndSetVisibility()
        {
            _currentUser = _authService.CurrentUser;

            IsAdminMenuVisible = false;
            IsDoctorMenuVisible = false;
            IsReceptionMenuVisible = false;
            IsPrescriptionMenuVisible = false;

            if (_currentUser != null)
            {
                if (_currentUser.Role == UserRole.Recepcionista || _currentUser.Role == UserRole.Doctor || _currentUser.Role == UserRole.Administrador)
                {
                    IsReceptionMenuVisible = true;
                }
                if (_currentUser.Role == UserRole.Doctor || _currentUser.Role == UserRole.Administrador)
                {
                    IsDoctorMenuVisible = true;
                    IsPrescriptionMenuVisible = true;
                }
                if (_currentUser.Role == UserRole.Administrador)
                {
                    IsAdminMenuVisible = true;
                }
            }
        }

        // --- CORRECCIÓN: Todos los comandos de navegación deben gestionar el ámbito ---

        [RelayCommand]
        private void NavigateToHome() // <-- AÑADIDO: Método para volver al inicio
        {
            _currentViewScope?.Dispose(); // Destruir el ámbito de la vista anterior
            _currentViewScope = null;     // No hay ámbito para el Singleton
            SelectedViewModel = _homeViewModel;
        }

        [RelayCommand]
        private void NavigateToPatients()
        {
            _currentViewScope?.Dispose();
            _currentViewScope = _scopeFactory.CreateScope();

            var patientsVM = _currentViewScope.ServiceProvider.GetRequiredService<PatientsViewModel>();
            patientsVM.SetNavigationCommand(NavigateToPatientFileCommand);
            SelectedViewModel = patientsVM;
        }

        [RelayCommand]
        private void NavigateToBudgets()
        {
            _currentViewScope?.Dispose();
            _currentViewScope = _scopeFactory.CreateScope();

            SelectedViewModel = _currentViewScope.ServiceProvider.GetRequiredService<BudgetsViewModel>();
        }

        [RelayCommand]
        private void NavigateToTreatments()
        {
            _currentViewScope?.Dispose();
            _currentViewScope = _scopeFactory.CreateScope();

            SelectedViewModel = _currentViewScope.ServiceProvider.GetRequiredService<TreatmentsViewModel>();
        }

        [RelayCommand]
        private void NavigateToPrescriptions()
        {
            _currentViewScope?.Dispose();
            _currentViewScope = _scopeFactory.CreateScope();

            SelectedViewModel = _currentViewScope.ServiceProvider.GetRequiredService<PrescriptionViewModel>();
        }

        [RelayCommand]
        private void NavigateToAdmin()
        {
            _currentViewScope?.Dispose();
            _currentViewScope = _scopeFactory.CreateScope();

            SelectedViewModel = _currentViewScope.ServiceProvider.GetRequiredService<AdminViewModel>();
        }

        [RelayCommand]
        private void NavigateToPatientFile()
        {
            // Esta vista es un Singleton, por lo que NO gestiona un ámbito.
            // Destruimos el ámbito anterior (ej. el de la lista de Pacientes).
            _currentViewScope?.Dispose();
            _currentViewScope = null;

            SelectedViewModel = _patientFileViewModel;
        }
    }
}