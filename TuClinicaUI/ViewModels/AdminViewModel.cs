// En: TuClinicaUI/ViewModels/AdminViewModel.cs

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TuClinica.Core.Interfaces;
using TuClinica.Core.Interfaces.Repositories; // Para IUserRepository, IRepository
using TuClinica.Core.Interfaces.Services;   // Para IBackupService
using TuClinica.Core.Models;                 // Para User, ActivityLog
using TuClinica.UI.Views;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;

namespace TuClinica.UI.ViewModels
{
    public partial class AdminViewModel : BaseViewModel
    {
        // --- Servicios Inyectados ---
        private readonly IUserRepository _userRepository;
        private readonly IServiceProvider _serviceProvider;
        private readonly IBackupService _backupService;
        private readonly IRepository<ActivityLog> _logRepository;
        private readonly IActivityLogService _activityLogService;
        private readonly IDialogService _dialogService; 
        private readonly IFileDialogService _fileDialogService;

        // --- Colecciones para la Vista ---
        [ObservableProperty]
        private ObservableCollection<User> _users = new();

        [ObservableProperty]
        private ObservableCollection<ActivityLog> _activityLogs = new(); // <--- NUEVO

        // --- Propiedad de Selección ---
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(EditUserCommand))]
        [NotifyCanExecuteChangedFor(nameof(ToggleUserActivationCommand))]
        private User? _selectedUser;

        // --- Comandos ---
        public IAsyncRelayCommand LoadLogsCommand { get; }
        public IAsyncRelayCommand ExportLogsCommand { get; }
        public IAsyncRelayCommand PurgeOldLogsCommand { get; }

        // --- CONSTRUCTOR MODIFICADO ---
        public AdminViewModel(
            IUserRepository userRepository,
            IServiceProvider serviceProvider,
            IBackupService backupService,
            IRepository<ActivityLog> logRepository, 
            IActivityLogService activityLogService,
            IDialogService dialogService,             
            IFileDialogService fileDialogService)
        {
            _userRepository = userRepository;
            _serviceProvider = serviceProvider;
            _backupService = backupService;
            _logRepository = logRepository;
            _activityLogService = activityLogService;
            _dialogService = dialogService;             
            _fileDialogService = fileDialogService;

            // Comandos
            LoadLogsCommand = new AsyncRelayCommand(LoadLogsAsync);
            ExportLogsCommand = new AsyncRelayCommand(ExportLogsAsync);
            PurgeOldLogsCommand = new AsyncRelayCommand(PurgeOldLogsAsync);

            // Carga inicial de datos
            _ = LoadUsersAsync();
            _ = LoadLogsAsync(); 
        }

        // --- Métodos de Carga ---

        [RelayCommand]
        private async Task LoadUsersAsync()
        {
            try
            {
                var userList = await _userRepository.GetAllAsync();
                Users.Clear();
                foreach (var user in userList.OrderBy(u => u.Username))
                {
                    Users.Add(user);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al cargar la lista de usuarios:\n{ex.Message}", "Error");
            }
        }

        // --- MÉTODO NUEVO PARA CARGAR LOGS ---
        private async Task LoadLogsAsync()
        {
            try
            {
                var logs = await _logRepository.GetAllAsync();
                ActivityLogs.Clear();
                // Ordenamos por fecha descendente para ver lo más nuevo primero
                foreach (var log in logs.OrderByDescending(l => l.Timestamp))
                {
                    ActivityLogs.Add(log);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al cargar el registro de actividad:\n{ex.Message}", "Error de Logs");
            }
        }
        // --- FIN MÉTODO NUEVO ---


        // --- Métodos de Gestión de Usuarios (EXISTENTES) ---

        [RelayCommand]
        private async Task AddNewUserAsync()
        {
            try
            {
                var dialog = _serviceProvider.GetRequiredService<UserEditDialog>();
                var viewModel = _serviceProvider.GetRequiredService<UserEditViewModel>();

                viewModel.LoadUserData(null);
                dialog.DataContext = viewModel;

                Window? owner = Application.Current.MainWindow;
                if (owner != null && owner != dialog)
                {
                    dialog.Owner = owner;
                }

                var result = dialog.ShowDialog();

                if (result == true)
                {
                    await LoadUsersAsync();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error fatal al intentar abrir el editor de usuarios:\n{ex.Message}\n\nStackTrace:\n{ex.StackTrace}",
                                "Error de Aplicación");
            }
        }

        private bool CanExecuteOnSelectedUser() => SelectedUser != null;

        [RelayCommand(CanExecute = nameof(CanExecuteOnSelectedUser))]
        private async Task EditUserAsync()
        {
            // ... (La lógica interna de AddNewUserAsync y EditUserAsync 
            //      ya usa servicios (GetRequiredService) y no MessageBox,
            //      por lo que está bien como está) ...
            try
            {
                if (SelectedUser == null) return;

                var dialog = _serviceProvider.GetRequiredService<UserEditDialog>();
                var viewModel = _serviceProvider.GetRequiredService<UserEditViewModel>();

                var userCopy = new User
                {
                    Id = SelectedUser.Id,
                    Username = SelectedUser.Username,
                    HashedPassword = SelectedUser.HashedPassword,
                    Role = SelectedUser.Role,
                    IsActive = SelectedUser.IsActive,
                    CollegeNumber = SelectedUser.CollegeNumber,
                    Specialty = SelectedUser.Specialty
                };

                viewModel.LoadUserData(userCopy);
                dialog.DataContext = viewModel;


                Window? owner = Application.Current.MainWindow;
                if (owner != null && owner != dialog)
                {
                    dialog.Owner = owner;
                }

                var result = dialog.ShowDialog();

                if (result == true)
                {
                    await LoadUsersAsync();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error fatal al intentar editar el usuario:\n{ex.Message}",
                                "Error de Aplicación");
            }
        }


        [RelayCommand(CanExecute = nameof(CanExecuteOnSelectedUser))]
        private async Task ToggleUserActivation()
        {
            if (SelectedUser == null) return;

            string action = SelectedUser.IsActive ? "desactivar" : "activar";
            var result = _dialogService.ShowConfirmation($"¿Estás seguro de que quieres {action} al usuario '{SelectedUser.Username}'?", "Confirmar");

            if (result == CoreDialogResult.Yes)
            {
                try
                {
                    SelectedUser.IsActive = !SelectedUser.IsActive;
                    _userRepository.Update(SelectedUser);
                    await _userRepository.SaveChangesAsync();
                    await LoadUsersAsync();
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Error al {action} al usuario:\n{ex.Message}", "Error");
                    if (SelectedUser != null) SelectedUser.IsActive = !SelectedUser.IsActive;
                }
            }
        }

        // --- Métodos de Backup (EXISTENTES) ---

        [RelayCommand]
        private async Task ExportBackupAsync()
        {
            try
            {
                // 1. Pedir Password
                var (passOk, password) = _dialogService.ShowPasswordPrompt();
                if (!passOk || string.IsNullOrWhiteSpace(password))
                {
                    return; // Usuario canceló o dejó vacío
                }

                // 2. Pedir Ruta para Guardar
                var (fileOk, filePath) = _fileDialogService.ShowSaveDialog(
                    filter: "Backup Files (*.bak)|*.bak",
                    title: "Guardar Copia de Seguridad",
                    defaultFileName: $"TuClinicaBackup_{DateTime.Now:yyyyMMdd_HHmm}.bak"
                );

                if (!fileOk)
                {
                    return; // Usuario canceló
                }

                // 3. Ejecutar Lógica
                bool success = await _backupService.ExportBackupAsync(filePath, password);

                // 4. Mostrar Resultado
                if (success)
                {
                    _dialogService.ShowMessage($"Copia guardada:\n{filePath}", "Éxito");
                }
                else
                {
                    _dialogService.ShowMessage("Error al exportar. La operación fue cancelada.", "Error");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error inesperado al exportar la copia:\n{ex.Message}", "Error Crítico");
            }
        }
        // --- LÓGICA DE EXPORTAR ---
        private async Task ExportLogsAsync()
        {
            var (ok, filePath) = _fileDialogService.ShowSaveDialog(
                filter: "Archivo CSV (*.csv)|*.csv",
                title: "Exportar Registro de Actividad",
                defaultFileName: $"TuClinica_Logs_{DateTime.Now:yyyyMMdd}.csv"
            );

            if (ok)
            {
                try
                {
                    string exportedPath = await _activityLogService.ExportLogsAsCsvAsync(filePath);
                    _dialogService.ShowMessage($"Logs exportados correctamente a:\n{exportedPath}", "Exportación Completa");
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Error al exportar los logs:\n{ex.Message}", "Error de Exportación");
                }
            }
        }

        // --- LÓGICA DE PURGAR ---
        private async Task PurgeOldLogsAsync()
        {
            int yearsToKeep = 2;
            var retentionDate = DateTime.UtcNow.AddYears(-yearsToKeep);

            var result = _dialogService.ShowConfirmation(
                $"¿Está seguro de que desea eliminar PERMANENTEMENTE todos los registros de logs anteriores al {retentionDate:dd/MM/yyyy}?\n\nEsta acción no se puede deshacer.",
                "Confirmar Purga de Logs"
            );

            if (result == CoreDialogResult.Yes)
            {
                try
                {
                    int deletedCount = await _activityLogService.PurgeOldLogsAsync(retentionDate);
                    _dialogService.ShowMessage($"Se han eliminado {deletedCount} registros antiguos.", "Purga Completa");
                    await LoadLogsAsync(); // Recargar la lista
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Error al purgar los logs:\n{ex.Message}", "Error de Purga");
                }
            }
        }
        [RelayCommand]
        private async Task ImportBackupAsync()
        {
            try
            {
                // 1. Pedir Archivo
                var (fileOk, filePath) = _fileDialogService.ShowOpenDialog(
                    filter: "Backup Files (*.bak)|*.bak",
                    title: "Abrir Copia de Seguridad"
                );

                if (!fileOk)
                {
                    return; // Usuario canceló
                }

                // 2. Confirmación de borrado
                var confirmation = _dialogService.ShowConfirmation(
                    "ADVERTENCIA: Esto BORRARÁ TODOS LOS DATOS ACTUALES.\n\n¿Continuar?",
                    "Confirmar Importación"
                );

                if (confirmation == CoreDialogResult.No)
                {
                    return;
                }

                // 3. Pedir Password
                var (passOk, password) = _dialogService.ShowPasswordPrompt();
                if (!passOk || string.IsNullOrWhiteSpace(password))
                {
                    _dialogService.ShowMessage("Se requiere una contraseña.", "Error");
                    return;
                }

                // 4. Ejecutar Lógica
                bool success = await _backupService.ImportBackupAsync(filePath, password);

                // 5. Mostrar Resultado
                if (success)
                {
                    _dialogService.ShowMessage("Importación completada.\nRecargando datos.", "Éxito");
                    await LoadUsersAsync();
                    await LoadLogsAsync();
                }
                else
                {
                    _dialogService.ShowMessage("Error al importar.\nContraseña incorrecta o archivo corrupto.", "Error");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error inesperado al importar la copia:\n{ex.Message}", "Error Crítico");
            }
        }
    }
}