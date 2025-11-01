using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Windows;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;

// 1. ASEGÚRATE DE QUE EL NAMESPACE ES ESTE
namespace TuClinica.UI.ViewModels
{
    public partial class MainWindowViewModel : BaseViewModel
    {
        private readonly IAuthService _authService;
        private readonly IServiceProvider _serviceProvider;
        private User? _currentUser;

        // 1. Guardamos una referencia al VM singleton
        private readonly PatientFileViewModel _patientFileViewModel;

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
                                   PatientFileViewModel patientFileViewModel) // <-- AÑADE ESTA INYECCIÓN
        {
            _authService = authService;
            _serviceProvider = serviceProvider;
            _patientFileViewModel = patientFileViewModel; // <-- AÑADE ESTA ASIGNACIÓN
            LoadCurrentUserAndSetVisibility();

            try
            {
                // 3. Modificamos cómo se crea PatientsViewModel
                var patientsVM = _serviceProvider.GetRequiredService<PatientsViewModel>();

                // 4. Le pasamos el comando de navegación al PatientsViewModel
                patientsVM.SetNavigationCommand(NavigateToPatientFileCommand);

                SelectedViewModel = patientsVM;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error crítico al cargar la vista inicial:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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