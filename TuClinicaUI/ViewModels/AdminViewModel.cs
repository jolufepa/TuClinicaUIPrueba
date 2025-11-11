// En: TuClinicaUI/ViewModels/AdminViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TuClinica.Core.Interfaces;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.UI.Views;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;
using System.Security.Cryptography;

namespace TuClinica.UI.ViewModels
{
    public partial class AdminViewModel : BaseViewModel
    {
        // --- Servicios Inyectados ---
        private readonly IUserRepository _userRepository;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IBackupService _backupService;
        private readonly IRepository<ActivityLog> _logRepository;
        private readonly IActivityLogService _activityLogService;
        private readonly IDialogService _dialogService;
        private readonly IFileDialogService _fileDialogService;

        // --- Colecciones para la Vista ---
        [ObservableProperty]
        private ObservableCollection<User> _users = new();

        [ObservableProperty]
        private ObservableCollection<ActivityLog> _activityLogs = new();

        // --- Propiedad de Selección (MODIFICADA) ---
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(EditUserCommand))]
        [NotifyCanExecuteChangedFor(nameof(ToggleUserActivationCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteUserAsyncCommand))] // <-- AÑADIDO
        private User? _selectedUser;

        // --- Comandos ---
        public IAsyncRelayCommand LoadLogsCommand { get; }
        public IAsyncRelayCommand ExportLogsCommand { get; }
        public IAsyncRelayCommand PurgeOldLogsCommand { get; }
        // --- COMANDO AÑADIDO ---
        public IAsyncRelayCommand DeleteUserAsyncCommand { get; }


        public AdminViewModel(
            IUserRepository userRepository,
            IServiceScopeFactory scopeFactory,
            IBackupService backupService,
            IRepository<ActivityLog> logRepository,
            IActivityLogService activityLogService,
            IDialogService dialogService,
            IFileDialogService fileDialogService)
        {
            _userRepository = userRepository;
            _scopeFactory = scopeFactory;
            _backupService = backupService;
            _logRepository = logRepository;
            _activityLogService = activityLogService;
            _dialogService = dialogService;
            _fileDialogService = fileDialogService;

            // Comandos
            LoadLogsCommand = new AsyncRelayCommand(LoadLogsAsync);
            ExportLogsCommand = new AsyncRelayCommand(ExportLogsAsync);
            PurgeOldLogsCommand = new AsyncRelayCommand(PurgeOldLogsAsync);
            // --- COMANDO AÑADIDO ---
            DeleteUserAsyncCommand = new AsyncRelayCommand(DeleteUserAsync, CanExecuteOnSelectedUser);


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

        private async Task LoadLogsAsync()
        {
            try
            {
                var logs = await _logRepository.GetAllAsync();
                ActivityLogs.Clear();
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

        // --- Métodos de Gestión de Usuarios (EXISTENTES) ---

        [RelayCommand]
        private async Task AddNewUserAsync()
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dialog = scope.ServiceProvider.GetRequiredService<UserEditDialog>();
                    var viewModel = scope.ServiceProvider.GetRequiredService<UserEditViewModel>();

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
            try
            {
                if (SelectedUser == null) return;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var dialog = scope.ServiceProvider.GetRequiredService<UserEditDialog>();
                    var viewModel = scope.ServiceProvider.GetRequiredService<UserEditViewModel>();

                    var userCopy = new User
                    {
                        Id = SelectedUser.Id,
                        Username = SelectedUser.Username,
                        HashedPassword = SelectedUser.HashedPassword,
                        Role = SelectedUser.Role,
                        IsActive = SelectedUser.IsActive,
                        CollegeNumber = SelectedUser.CollegeNumber,
                        Specialty = SelectedUser.Specialty,
                        Name = SelectedUser.Name
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
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error fatal al intentar editar el usuario:\n{ex.Message}",
                                "Error de Aplicación");
            }
        }


        // --- MÉTODO CORREGIDO ---
        [RelayCommand(CanExecute = nameof(CanExecuteOnSelectedUser))]
        private async Task ToggleUserActivation()
        {
            if (SelectedUser == null) return;

            if (SelectedUser.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                _dialogService.ShowMessage("No se puede desactivar al usuario administrador principal 'admin'.", "Acción no permitida");
                await LoadUsersAsync(); // Recarga para revertir el clic visual
                return;
            }

            // Leemos el estado ANTES de preguntar, porque 'SelectedUser' es de la UI
            // y el DataGrid puede haberlo cambiado visualmente antes de que confirmemos.
            bool wasActive = SelectedUser.IsActive;
            string action = wasActive ? "desactivar" : "activar";

            var result = _dialogService.ShowConfirmation($"¿Estás seguro de que quieres {action} al usuario '{SelectedUser.Username}'?", "Confirmar");

            if (result == CoreDialogResult.Yes)
            {
                try
                {
                    // --- INICIO DE LA LÓGICA CORREGIDA ---
                    // 1. Obtener la entidad rastreada desde la BD
                    var userToUpdate = await _userRepository.GetByIdAsync(SelectedUser.Id);

                    if (userToUpdate != null)
                    {
                        // 2. Modificar la entidad rastreada
                        // Usamos el estado que leímos (wasActive)
                        userToUpdate.IsActive = !wasActive;

                        // 3. Guardar cambios (Update() NO es necesario)
                        await _userRepository.SaveChangesAsync();

                        _dialogService.ShowMessage($"Usuario '{userToUpdate.Username}' {action}do con éxito.", "Éxito");

                        // 4. Recargar la lista
                        await LoadUsersAsync();
                    }
                    else
                    {
                        _dialogService.ShowMessage("Error: No se encontró el usuario a modificar.", "Error");
                        await LoadUsersAsync();
                    }
                    // --- FIN DE LA LÓGICA CORREGIDA ---
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Error al {action} al usuario:\n{ex.Message}", "Error");
                    // Recargar la lista para revertir visualmente
                    await LoadUsersAsync();
                }
            }
            else
            {
                // Si el usuario presiona "No", recargamos para revertir el clic en el CheckBox
                await LoadUsersAsync();
            }
        }

        // --- NUEVO MÉTODO AÑADIDO (Lógica de borrado) ---
        [RelayCommand(CanExecute = nameof(CanExecuteOnSelectedUser))]
        private async Task DeleteUserAsync()
        {
            if (SelectedUser == null) return;

            // 1. Comprobar si es 'admin'
            if (SelectedUser.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                _dialogService.ShowMessage("No se puede eliminar al usuario administrador principal 'admin'.", "Acción no permitida");
                return;
            }

            // 2. Confirmación
            var result = _dialogService.ShowConfirmation(
                $"¿Estás seguro de que quieres eliminar PERMANENTEMENTE al usuario '{SelectedUser.Username}'?\n\nEsta acción no se puede deshacer.",
                "Confirmar Eliminación Permanente");

            if (result == CoreDialogResult.No) return;

            // 3. Lógica de borrado
            try
            {
                // Usamos GetByIdAsync para asegurarnos de que tenemos la entidad rastreada
                var userToDelete = await _userRepository.GetByIdAsync(SelectedUser.Id);
                if (userToDelete != null)
                {
                    _userRepository.Remove(userToDelete);
                    await _userRepository.SaveChangesAsync();
                    _dialogService.ShowMessage($"Usuario '{userToDelete.Username}' eliminado con éxito.", "Eliminado");
                    await LoadUsersAsync(); // Recargar la lista
                    SelectedUser = null; // Limpiar selección
                }
                else
                {
                    _dialogService.ShowMessage("El usuario seleccionado no se encontró en la base de datos (quizás ya fue eliminado).", "Error");
                    await LoadUsersAsync();
                }
            }
            catch (Exception ex)
            {
                // Capturar errores (ej. si el usuario tiene claves foráneas en otras tablas)
                _dialogService.ShowMessage($"Error al eliminar al usuario:\n{ex.Message}\n\nEs posible que este usuario tenga historial clínico (cargos) y no pueda ser borrado.", "Error de Base de Datos");
            }
        }


        // --- Métodos de Backup (Sin cambios) ---

        [RelayCommand]
        private async Task ExportBackupAsync()
        {
            try
            {
                var (passOk, password) = _dialogService.ShowPasswordPrompt();
                if (!passOk || string.IsNullOrWhiteSpace(password))
                {
                    return;
                }

                var (fileOk, filePath) = _fileDialogService.ShowSaveDialog(
                    filter: "Backup Files (*.bak)|*.bak",
                    title: "Guardar Copia de Seguridad",
                    defaultFileName: $"TuClinicaBackup_{DateTime.Now:yyyyMMdd_HHmm}.bak"
                );

                if (!fileOk)
                {
                    return;
                }

                bool success = await _backupService.ExportBackupAsync(filePath, password);

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
                _dialogService.ShowMessage($"Error inesperado al exportar la copia:\n\n{ex.Message}", "Error Crítico");
            }
        }

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
                var (fileOk, filePath) = _fileDialogService.ShowOpenDialog(
                    filter: "Backup Files (*.bak)|*.bak",
                    title: "Abrir Copia de Seguridad"
                );

                if (!fileOk)
                {
                    return;
                }

                var confirmation = _dialogService.ShowConfirmation(
                    "ADVERTENCIA: Esto BORRARÁ TODOS LOS DATOS ACTUALES.\n\n¿Continuar?",
                    "Confirmar Importación"
                );

                if (confirmation == CoreDialogResult.No)
                {
                    return;
                }

                var (passOk, password) = _dialogService.ShowPasswordPrompt();
                if (!passOk || string.IsNullOrWhiteSpace(password))
                {
                    _dialogService.ShowMessage("Se requiere una contraseña.", "Error");
                    return;
                }

                bool success = await _backupService.ImportBackupAsync(filePath, password);

                if (success)
                {
                    _dialogService.ShowMessage("Importación completada.\nRecargando datos.", "Éxito");
                    await LoadUsersAsync();
                    await LoadLogsAsync();
                }
                else
                {
                    _dialogService.ShowMessage("Error al importar.", "Error");
                }
            }
            catch (CryptographicException)
            {
                _dialogService.ShowMessage("Error al importar.\nContraseña incorrecta o archivo corrupto.", "Error de Importación");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error inesperado al importar la copia:\n\n{ex.Message}\n\nInnerException:\n{ex.InnerException?.Message}", "Error Crítico");
            }
        }
    }
}