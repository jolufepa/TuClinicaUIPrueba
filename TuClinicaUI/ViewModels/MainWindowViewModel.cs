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

        private IServiceScope? _currentViewScope;

        private readonly PatientFileViewModel _patientFileViewModel;
        private readonly HomeViewModel _homeViewModel;

        [ObservableProperty] private bool _isAdminMenuVisible;
        [ObservableProperty] private bool _isDoctorMenuVisible;
        [ObservableProperty] private bool _isReceptionMenuVisible;
        [ObservableProperty] private bool _isPrescriptionMenuVisible;

        // Nueva propiedad para visibilidad del menú de finanzas
        [ObservableProperty] private bool _isFinancialMenuVisible;

        [ObservableProperty] private BaseViewModel? _selectedViewModel;

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

            SelectedViewModel = _homeViewModel;

            WeakReferenceMessenger.Default.Register(this);
        }

        public void Receive(NavigateToNewBudgetMessage message)
        {
            _currentViewScope?.Dispose();
            _currentViewScope = _scopeFactory.CreateScope();
            var budgetVM = _currentViewScope.ServiceProvider.GetRequiredService<BudgetsViewModel>();
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
            IsFinancialMenuVisible = false;

            if (_currentUser != null)
            {
                if (_currentUser.Role == UserRole.Recepcionista || _currentUser.Role == UserRole.Doctor || _currentUser.Role == UserRole.Administrador)
                {
                    IsReceptionMenuVisible = true;
                }

                // --- AQUÍ ESTÁ EL CAMBIO ---
                // Doctores y Administradores pueden ver Tratamientos, Recetas y AHORA EL RESUMEN FINANCIERO
                if (_currentUser.Role == UserRole.Doctor || _currentUser.Role == UserRole.Administrador)
                {
                    IsDoctorMenuVisible = true;
                    IsPrescriptionMenuVisible = true;
                    IsFinancialMenuVisible = true; // Habilitado para Doctores también
                }

                if (_currentUser.Role == UserRole.Administrador)
                {
                    IsAdminMenuVisible = true;
                }
            }
        }

        [RelayCommand]
        private void NavigateToHome()
        {
            _currentViewScope?.Dispose();
            _currentViewScope = null;
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
        private void NavigateToFinancialSummary()
        {
            _currentViewScope?.Dispose();
            _currentViewScope = _scopeFactory.CreateScope();
            SelectedViewModel = _currentViewScope.ServiceProvider.GetRequiredService<FinancialSummaryViewModel>();
        }

        [RelayCommand]
        private void NavigateToPatientFile()
        {
            _currentViewScope?.Dispose();
            _currentViewScope = null;
            SelectedViewModel = _patientFileViewModel;
        }
    }
}