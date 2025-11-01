// En: TuClinicaUI/ViewModels/AdminViewModel.cs

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
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
            IActivityLogService activityLogService)
        {
            _userRepository = userRepository;
            _serviceProvider = serviceProvider;
            _backupService = backupService;
            _logRepository = logRepository;
            _activityLogService = activityLogService;

            // Comandos
            LoadLogsCommand = new AsyncRelayCommand(LoadLogsAsync);
            ExportLogsCommand = new AsyncRelayCommand(ExportLogsAsync);
            PurgeOldLogsCommand = new AsyncRelayCommand(PurgeOldLogsAsync);

            // Carga inicial de datos
            _ = LoadUsersAsync();
            _ = LoadLogsAsync(); // <--- NUEVA CARGA INICIAL
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
                MessageBox.Show($"Error al cargar la lista de usuarios:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Error al cargar el registro de actividad:\n{ex.Message}", "Error de Logs", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Error fatal al intentar abrir el editor de usuarios:\n{ex.Message}\n\nStackTrace:\n{ex.StackTrace}",
                                "Error de Aplicación",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private bool CanExecuteOnSelectedUser() => SelectedUser != null;

        [RelayCommand(CanExecute = nameof(CanExecuteOnSelectedUser))]
        private async Task EditUserAsync()
        {
            try
            {
                if (SelectedUser == null) return;

                var dialog = _serviceProvider.GetRequiredService<UserEditDialog>();
                var viewModel = _serviceProvider.GetRequiredService<UserEditViewModel>();

                // Copiamos todas las propiedades, incluyendo las nuevas de doctor
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
                MessageBox.Show($"Error fatal al intentar editar el usuario:\n{ex.Message}",
                                "Error de Aplicación",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }


        [RelayCommand(CanExecute = nameof(CanExecuteOnSelectedUser))]
        private async Task ToggleUserActivation()
        {
            if (SelectedUser == null) return;

            if (!SelectedUser.IsActive && SelectedUser.Role == Core.Enums.UserRole.Administrador)
            {
                // (Aquí podrías añadir una lógica para evitar desactivar/eliminar el último admin)
            }

            string action = SelectedUser.IsActive ? "desactivar" : "activar";
            var result = MessageBox.Show($"¿Estás seguro de que quieres {action} al usuario '{SelectedUser.Username}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    SelectedUser.IsActive = !SelectedUser.IsActive;
                    // Usamos el Update síncrono y SaveChangesAsync para que
                    // el DbContext pueda interceptar el cambio (como en PatientsViewModel).
                    _userRepository.Update(SelectedUser);
                    await _userRepository.SaveChangesAsync();
                    await LoadUsersAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al {action} al usuario:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    // Revertir cambio local si falla
                    if (SelectedUser != null) SelectedUser.IsActive = !SelectedUser.IsActive;
                }
            }
        }

        // --- Métodos de Backup (EXISTENTES) ---

        [RelayCommand]
        private async Task ExportBackupAsync()
        {
            PasswordPromptDialog? passwordDialog = null;
            SaveFileDialog? saveFileDialog = null;

            try
            {
                try
                {
                    passwordDialog = new PasswordPromptDialog();

                    Window? owner = Application.Current.MainWindow;
                    if (owner != null && owner != passwordDialog)
                    {
                        passwordDialog.Owner = owner;
                    }

                    bool? passwordResult = passwordDialog.ShowDialog();

                    if (passwordResult != true)
                    {
                        return;
                    }
                }
                catch (Exception pwdEx)
                {
                    MessageBox.Show($"Error al preparar la solicitud de contraseña:\n{pwdEx.Message}", "Error Interno", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string password = passwordDialog.Password;
                if (string.IsNullOrWhiteSpace(password))
                {
                    return;
                }

                try
                {
                    saveFileDialog = new SaveFileDialog
                    {
                        Filter = "Backup Files (*.bak)|*.bak",
                        Title = "Guardar Copia de Seguridad",
                        FileName = $"TuClinicaBackup_{DateTime.Now:yyyyMMdd_HHmm}.bak"
                    };

                    bool? saveResult = saveFileDialog.ShowDialog();

                    if (saveResult != true)
                    {
                        return;
                    }
                }
                catch (Exception fileEx)
                {
                    MessageBox.Show($"Error al preparar la selección de archivo:\n{fileEx.Message}", "Error Interno", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string filePath = saveFileDialog.FileName;

                bool success = false;
                try
                {
                    success = await _backupService.ExportBackupAsync(filePath, password);
                }
                catch (Exception backupEx)
                {
                    MessageBox.Show($"Error durante el proceso de exportación:\n{backupEx.Message}", "Error de Exportación", MessageBoxButton.OK, MessageBoxImage.Error);
                    success = false;
                }

                MessageBox.Show(success ? $"Copia guardada:\n{filePath}" : "Error al exportar.", success ? "Éxito" : "Error", MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Error);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error inesperado al iniciar la exportación:\n{ex.Message}", "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // --- LÓGICA DE EXPORTAR ---
        private async Task ExportLogsAsync()
        {
            // 1. Pide al usuario dónde guardar el archivo
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Archivo CSV (*.csv)|*.csv",
                Title = "Exportar Registro de Actividad",
                FileName = $"TuClinica_Logs_{DateTime.Now:yyyyMMdd}.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // 2. Llama al servicio con la ruta seleccionada
                    string filePath = await _activityLogService.ExportLogsAsCsvAsync(saveFileDialog.FileName);
                    MessageBox.Show($"Logs exportados correctamente a:\n{filePath}", "Exportación Completa", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al exportar los logs:\n{ex.Message}", "Error de Exportación", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- LÓGICA DE PURGAR ---
        private async Task PurgeOldLogsAsync()
        {
            // 1. Define la política de 2 AÑOS que pediste
            int yearsToKeep = 2;
            var retentionDate = DateTime.UtcNow.AddYears(-yearsToKeep);

            // 2. Pide confirmación al usuario (¡muy importante!)
            var result = MessageBox.Show($"¿Está seguro de que desea eliminar PERMANENTEMENTE todos los registros de logs anteriores al {retentionDate:dd/MM/yyyy}?\n\nEsta acción no se puede deshacer.",
                                         "Confirmar Purga de Logs",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // 3. Llama al servicio con la fecha límite
                    int deletedCount = await _activityLogService.PurgeOldLogsAsync(retentionDate);
                    MessageBox.Show($"Se han eliminado {deletedCount} registros antiguos.", "Purga Completa", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 4. Recarga la lista en la UI
                    await LoadLogsAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al purgar los logs:\n{ex.Message}", "Error de Purga", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        [RelayCommand]
        private async Task ImportBackupAsync()
        {
            OpenFileDialog? openFileDialog = null;
            PasswordPromptDialog? passwordDialog = null;

            try
            {
                try
                {
                    openFileDialog = new OpenFileDialog
                    {
                        Filter = "Backup Files (*.bak)|*.bak",
                        Title = "Abrir Copia de Seguridad"
                    };

                    bool? openResult = openFileDialog.ShowDialog();

                    if (openResult != true)
                    {
                        return;
                    }
                }
                catch (Exception fileEx)
                {
                    MessageBox.Show($"Error al preparar la selección de archivo:\n{fileEx.Message}", "Error Interno", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string filePath = openFileDialog.FileName;

                var confirmation = MessageBox.Show(
                    "ADVERTENCIA: Esto BORRARÁ TODOS LOS DATOS ACTUALES.\n\n¿Continuar?",
                    "Confirmar Importación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirmation != MessageBoxResult.Yes)
                {
                    return;
                }

                string password = string.Empty;
                try
                {
                    passwordDialog = new PasswordPromptDialog();

                    Window? owner = Application.Current.MainWindow;
                    if (owner != null && owner != passwordDialog)
                    {
                        passwordDialog.Owner = owner;
                    }

                    bool? passwordResult = passwordDialog.ShowDialog();

                    if (passwordResult != true)
                    {
                        return;
                    }

                    password = passwordDialog.Password;
                    if (string.IsNullOrWhiteSpace(password))
                    {
                        MessageBox.Show("Se requiere una contraseña.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                catch (Exception pwdEx)
                {
                    MessageBox.Show($"Error al solicitar la contraseña:\n{pwdEx.Message}", "Error Interno", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                bool success = false;
                try
                {
                    success = await _backupService.ImportBackupAsync(filePath, password);
                }
                catch (Exception importEx)
                {
                    MessageBox.Show($"Error durante el proceso de importación:\n{importEx.Message}", "Error de Importación", MessageBoxButton.OK, MessageBoxImage.Error);
                    success = false;
                }

                MessageBox.Show(success ? "Importación completada.\nRecargando datos." : "Error al importar.\nContraseña incorrecta o archivo corrupto.", success ? "Éxito" : "Error", MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Error);

                if (success)
                {
                    await LoadUsersAsync(); // Recargar usuarios si éxito
                    await LoadLogsAsync();  // Recargar logs también
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error inesperado al iniciar la importación:\n{ex.Message}", "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}