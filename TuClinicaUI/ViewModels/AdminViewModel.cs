using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
        private readonly IUserRepository _userRepository;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IBackupService _backupService;
        private readonly IRepository<ActivityLog> _logRepository;
        private readonly IActivityLogService _activityLogService;
        private readonly IDialogService _dialogService;
        private readonly IFileDialogService _fileDialogService;
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        private ObservableCollection<User> _users = new();

        [ObservableProperty]
        private ObservableCollection<ActivityLog> _activityLogs = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(EditUserCommand))]
        [NotifyCanExecuteChangedFor(nameof(ToggleUserActivationCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteUserAsyncCommand))]
        private User? _selectedUser;

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

        [ObservableProperty]
        private bool isBusy;

        public IAsyncRelayCommand LoadLogsCommand { get; }
        public IAsyncRelayCommand ExportLogsCommand { get; }
        public IAsyncRelayCommand PurgeOldLogsCommand { get; }
        public IAsyncRelayCommand DeleteUserAsyncCommand { get; }
        public IAsyncRelayCommand SaveSettingsCommand { get; }
        public IAsyncRelayCommand CreateBackupCommand { get; }
        public IAsyncRelayCommand RestoreBackupCommand { get; }

        public AdminViewModel(
            IUserRepository userRepository,
            IServiceScopeFactory scopeFactory,
            IBackupService backupService,
            IRepository<ActivityLog> logRepository,
            IActivityLogService activityLogService,
            IDialogService dialogService,
            IFileDialogService fileDialogService,
            ISettingsService settingsService
            )
        {
            _userRepository = userRepository;
            _scopeFactory = scopeFactory;
            _backupService = backupService;
            _logRepository = logRepository;
            _activityLogService = activityLogService;
            _dialogService = dialogService;
            _fileDialogService = fileDialogService;
            _settingsService = settingsService;

            LoadLogsCommand = new AsyncRelayCommand(LoadLogsAsync);
            ExportLogsCommand = new AsyncRelayCommand(ExportLogsAsync);
            PurgeOldLogsCommand = new AsyncRelayCommand(PurgeOldLogsAsync);
            DeleteUserAsyncCommand = new AsyncRelayCommand(DeleteUserAsync, CanExecuteOnSelectedUser);
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
            CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync);
            RestoreBackupCommand = new AsyncRelayCommand(RestoreBackupAsync);

            _ = LoadUsersAsync();
            _ = LoadLogsAsync();
            LoadSettings();
        }

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


        [RelayCommand(CanExecute = nameof(CanExecuteOnSelectedUser))]
        private async Task ToggleUserActivation()
        {
            if (SelectedUser == null) return;

            if (SelectedUser.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                _dialogService.ShowMessage("No se puede desactivar al usuario administrador principal 'admin'.", "Acción no permitida");
                await LoadUsersAsync();
                return;
            }

            bool wasActive = SelectedUser.IsActive;
            string action = wasActive ? "desactivar" : "activar";

            var result = _dialogService.ShowConfirmation($"¿Estás seguro de que quieres {action} al usuario '{SelectedUser.Username}'?", "Confirmar");

            if (result == CoreDialogResult.Yes)
            {
                try
                {
                    var userToUpdate = await _userRepository.GetByIdAsync(SelectedUser.Id);

                    if (userToUpdate != null)
                    {
                        userToUpdate.IsActive = !wasActive;

                        await _userRepository.SaveChangesAsync();

                        _dialogService.ShowMessage($"Usuario '{userToUpdate.Username}' {action}do con éxito.", "Éxito");

                        await LoadUsersAsync();
                    }
                    else
                    {
                        _dialogService.ShowMessage("Error: No se encontró el usuario a modificar.", "Error");
                        await LoadUsersAsync();
                    }
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Error al {action} al usuario:\n{ex.Message}", "Error");
                    await LoadUsersAsync();
                }
            }
            else
            {
                await LoadUsersAsync();
            }
        }

        [RelayCommand(CanExecute = nameof(CanExecuteOnSelectedUser))]
        private async Task DeleteUserAsync()
        {
            if (SelectedUser == null) return;

            if (SelectedUser.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                _dialogService.ShowMessage("No se puede eliminar al usuario administrador principal 'admin'.", "Acción no permitida");
                return;
            }

            var result = _dialogService.ShowConfirmation(
                $"¿Estás seguro de que quieres eliminar PERMANENTEMENTE al usuario '{SelectedUser.Username}'?\n\nEsta acción no se puede deshacer.",
                "Confirmar Eliminación Permanente");

            if (result == CoreDialogResult.No) return;

            try
            {
                var userToDelete = await _userRepository.GetByIdAsync(SelectedUser.Id);
                if (userToDelete != null)
                {
                    _userRepository.Remove(userToDelete);
                    await _userRepository.SaveChangesAsync();
                    _dialogService.ShowMessage($"Usuario '{userToDelete.Username}' eliminado con éxito.", "Eliminado");
                    await LoadUsersAsync();
                    SelectedUser = null;
                }
                else
                {
                    _dialogService.ShowMessage("El usuario seleccionado no se encontró en la base de datos (quizás ya fue eliminado).", "Error");
                    await LoadUsersAsync();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al eliminar al usuario:\n{ex.Message}\n\nEs posible que este usuario tenga historial clínico (cargos) y no pueda ser borrado.", "Error de Base de Datos");
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
                    await LoadLogsAsync();
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Error al purgar los logs:\n{ex.Message}", "Error de Purga");
                }
            }
        }
        private async Task CreateBackupAsync()
        {
            IsBusy = true;
            try
            {
                string password;
                do
                {
                    var (passOk, tempPass) = _dialogService.ShowPasswordPrompt();
                    if (!passOk || string.IsNullOrWhiteSpace(tempPass))
                    {
                        return;
                    }
                    password = tempPass;

                    if (password.Length < 8 || !password.Any(char.IsDigit) || !password.Any(char.IsLetter))
                    {
                        _dialogService.ShowMessage("La contraseña debe tener al menos 8 caracteres, incluyendo letras y números.", "Contraseña Inválida");
                        password = null;
                    }
                } while (password == null);

                var (fileOk, filePath) = _fileDialogService.ShowSaveDialog(
                    filter: "Respaldo TuClinica (*.tcb)|*.tcb",
                    title: "Guardar Copia de Seguridad Blindada",
                    defaultFileName: $"TuClinica_FullBackup_{DateTime.Now:yyyyMMdd}.tcb"
                );

                if (fileOk)
                {
                    await _backupService.CreateBackupAsync(filePath, password);
                    await _activityLogService.LogAccessAsync("Backup creado en " + filePath);
                    _dialogService.ShowMessage($"Copia de seguridad ENCRIPTADA guardada en:\n{filePath}", "Seguridad");
                }
            }
            catch (CryptographicException ex)
            {
                _dialogService.ShowMessage($"Error de encriptación (verifique contraseña): {ex.Message}", "Error");
            }
            catch (IOException ex)
            {
                _dialogService.ShowMessage($"Error de archivo/disco: {ex.Message}", "Error");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al crear el backup: {ex.Message}", "Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RestoreBackupAsync()
        {
            IsBusy = true;
            try
            {
                var confirmation = _dialogService.ShowConfirmation(
                    "PELIGRO: Esta acción BORRARÁ TODOS LOS DATOS ACTUALES.\n\nSe requerirá la contraseña de la copia para desencriptarla.\n\n¿Continuar?",
                    "Restauración Segura"
                );

                if (confirmation == CoreDialogResult.No) return;

                var (fileOk, filePath) = _fileDialogService.ShowOpenDialog(
                    filter: "Respaldo TuClinica (*.tcb)|*.tcb",
                    title: "Seleccionar Copia de Seguridad"
                );

                if (fileOk)
                {
                    string password;
                    do
                    {
                        var (passOk, tempPass) = _dialogService.ShowPasswordPrompt();
                        if (!passOk || string.IsNullOrWhiteSpace(tempPass))
                        {
                            _dialogService.ShowMessage("Se requiere contraseña para restaurar.", "Seguridad");
                            return;
                        }
                        password = tempPass;

                        if (password.Length < 8 || !password.Any(char.IsDigit) || !password.Any(char.IsLetter))
                        {
                            _dialogService.ShowMessage("La contraseña debe tener al menos 8 caracteres, incluyendo letras y números.", "Contraseña Inválida");
                            password = null;
                        }
                    } while (password == null);

                    await _backupService.RestoreBackupAsync(filePath, password);
                    await _activityLogService.LogAccessAsync("Backup restaurado desde " + filePath);
                }
            }
            catch (CryptographicException ex)
            {
                _dialogService.ShowMessage($"Contraseña incorrecta o archivo dañado: {ex.Message}", "Error Crítico");
            }
            catch (IOException ex)
            {
                _dialogService.ShowMessage($"Error de archivo/disco: {ex.Message}", "Error Crítico");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al restaurar: {ex.Message}", "Error Crítico");
            }
            finally
            {
                IsBusy = false;
            }
        }

    }
}