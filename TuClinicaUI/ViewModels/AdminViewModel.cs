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
        // --- INICIO DE LA MODIFICACIÓN ---
        private readonly ISettingsService _settingsService;
        // --- FIN DE LA MODIFICACIÓN ---

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

        // --- INICIO DE LA MODIFICACIÓN (Propiedades de Configuración) ---
        [ObservableProperty]
        private string _clinicName = string.Empty;
        [ObservableProperty]
        private string _clinicCif = string.Empty;
        [ObservableProperty]
        private string _clinicAddress = string.Empty;
        [ObservableProperty]
        private string _clinicPhone = string.Empty;
        [ObservableProperty]
        private string _clinicLogoPath = string.Empty;
        [ObservableProperty]
        private string _clinicEmail = string.Empty;
        // --- FIN DE LA MODIFICACIÓN ---

        // --- Comandos ---
        public IAsyncRelayCommand LoadLogsCommand { get; }
        public IAsyncRelayCommand ExportLogsCommand { get; }
        public IAsyncRelayCommand PurgeOldLogsCommand { get; }
        public IAsyncRelayCommand DeleteUserAsyncCommand { get; }
        // --- INICIO DE LA MODIFICACIÓN ---
        public IAsyncRelayCommand SaveSettingsCommand { get; }
        // --- FIN DE LA MODIFICACIÓN ---
        public IAsyncRelayCommand CreateBackupCommand { get; } // Antes Export
        public IAsyncRelayCommand RestoreBackupCommand { get; }

        public AdminViewModel(
            IUserRepository userRepository,
            IServiceScopeFactory scopeFactory,
            IBackupService backupService,
            IRepository<ActivityLog> logRepository,
            IActivityLogService activityLogService,
            IDialogService dialogService,
            IFileDialogService fileDialogService,
            // --- INICIO DE LA MODIFICACIÓN ---
            ISettingsService settingsService // Inyectar el nuevo servicio
                                             // --- FIN DE LA MODIFICACIÓN ---
            )
        {
            _userRepository = userRepository;
            _scopeFactory = scopeFactory;
            _backupService = backupService;
            _logRepository = logRepository;
            _activityLogService = activityLogService;
            _dialogService = dialogService;
            _fileDialogService = fileDialogService;
            // --- INICIO DE LA MODIFICACIÓN ---
            _settingsService = settingsService;
            // --- FIN DE LA MODIFICACIÓN ---

            // Comandos
            LoadLogsCommand = new AsyncRelayCommand(LoadLogsAsync);
            ExportLogsCommand = new AsyncRelayCommand(ExportLogsAsync);
            PurgeOldLogsCommand = new AsyncRelayCommand(PurgeOldLogsAsync);
            DeleteUserAsyncCommand = new AsyncRelayCommand(DeleteUserAsync, CanExecuteOnSelectedUser);
            // --- INICIO DE LA MODIFICACIÓN ---
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
            // --- FIN DE LA MODIFICACIÓN ---
            CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync);
            RestoreBackupCommand = new AsyncRelayCommand(RestoreBackupAsync);

            // Carga inicial de datos
            _ = LoadUsersAsync();
            _ = LoadLogsAsync();
            // --- INICIO DE LA MODIFICACIÓN ---
            LoadSettings(); // Cargar la configuración de la clínica
            // --- FIN DE LA MODIFICACIÓN ---
        }

        // --- INICIO DE LA MODIFICACIÓN (Nuevos Métodos) ---

        /// <summary>
        /// Carga la configuración actual desde el servicio a las propiedades del ViewModel.
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                var settings = _settingsService.GetSettings();
                ClinicName = settings.ClinicName;
                ClinicCif = settings.ClinicCif;
                ClinicAddress = settings.ClinicAddress;
                ClinicPhone = settings.ClinicPhone;
                ClinicLogoPath = settings.ClinicLogoPath;
                ClinicEmail = settings.ClinicEmail;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al cargar la configuración: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// Guarda la configuración modificada desde el ViewModel al archivo appsettings.json.
        /// </summary>
        private async Task SaveSettingsAsync()
        {
            var settings = new AppSettings
            {
                ClinicName = this.ClinicName,
                ClinicCif = this.ClinicCif,
                ClinicAddress = this.ClinicAddress,
                ClinicPhone = this.ClinicPhone,
                ClinicLogoPath = this.ClinicLogoPath,
                ClinicEmail = this.ClinicEmail
            };

            var result = _dialogService.ShowConfirmation(
                "¿Está seguro de que desea guardar los cambios en la configuración de la clínica?\n\nLa aplicación debe reiniciarse para que todos los cambios surtan efecto.",
                "Confirmar Guardado");

            if (result == CoreDialogResult.No)
                return;

            try
            {
                bool success = await _settingsService.SaveSettingsAsync(settings);
                if (success)
                {
                    _dialogService.ShowMessage("Configuración guardada. Por favor, reinicie la aplicación.", "Éxito");
                }
                else
                {
                    _dialogService.ShowMessage("No se pudo guardar la configuración.", "Error");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al guardar la configuración: {ex.Message}", "Error Crítico");
            }
        }
        // --- FIN DE LA MODIFICACIÓN ---


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
        private async Task CreateBackupAsync()
        {
            // 1. Pedir Contraseña al usuario para encriptar
            var (passOk, password) = _dialogService.ShowPasswordPrompt();
            if (!passOk || string.IsNullOrWhiteSpace(password))
            {
                // Si cancela o la deja vacía, abortamos por seguridad
                return;
            }

            // 2. Diálogo guardar archivo (ahora sugerimos extensión .bak para denotar que es backup cifrado, aunque puede ser .zip.enc)
            // Mantenemos .zip por familiaridad, pero el contenido estará cifrado.
            // En AdminViewModel.cs

            var (fileOk, filePath) = _fileDialogService.ShowSaveDialog(
                filter: "Copia de Seguridad (*.db)|*.db", // Cambiado a .db
                title: "Guardar Copia de Seguridad Portable",
                defaultFileName: $"TuClinica_Backup_{DateTime.Now:yyyyMMdd}.db" // Cambiado a .db
            );

            if (fileOk)
            {
                try
                {
                    // 3. Llamar al servicio CON contraseña
                    await _backupService.CreateBackupAsync(filePath, password);
                    _dialogService.ShowMessage($"Copia de seguridad ENCRIPTADA guardada en:\n{filePath}", "Seguridad");
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Error al crear el backup:\n{ex.Message}", "Error");
                }
            }
        }

        private async Task RestoreBackupAsync()
        {
            var confirmation = _dialogService.ShowConfirmation(
                "PELIGRO: Esta acción BORRARÁ TODOS LOS DATOS ACTUALES.\n\nSe requerirá la contraseña de la copia para desencriptarla.\n\n¿Continuar?",
                "Restauración Segura"
            );

            if (confirmation == CoreDialogResult.No) return;

            var (fileOk, filePath) = _fileDialogService.ShowOpenDialog(
                    filter: "Copia de Seguridad (*.db)|*.db", // Cambiado a .db
                    title: "Seleccionar Copia de Seguridad"
                );

            if (fileOk)
            {
                // 1. Pedir contraseña de desencriptado
                var (passOk, password) = _dialogService.ShowPasswordPrompt();
                if (!passOk || string.IsNullOrWhiteSpace(password))
                {
                    _dialogService.ShowMessage("Se requiere contraseña para restaurar.", "Seguridad");
                    return;
                }

                try
                {
                    // 2. Restaurar CON contraseña
                    await _backupService.RestoreBackupAsync(filePath, password);
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Error al restaurar (Verifique su contraseña):\n{ex.Message}", "Error Crítico");
                }
            }
        }

    }
}