// --- Using Esenciales ---
using BCrypt.Net; // Para BCrypt
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting; // Para IHost, Host
using Microsoft.Extensions.Options; // Para IOptions
using SQLitePCL; // Para Batteries
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces;
// --- Using de Interfaces ---
using TuClinica.Core.Interfaces.Repositories; // *** PARA IRepository<> ***
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.DataAccess; // Para AppDbContext
using TuClinica.DataAccess.Repositories; // Para Implementaciones de Repositorio
using TuClinica.Services.Implementation; // Para Implementaciones de Servicio
using TuClinica.UI.ViewModels; // Para ViewModels
using TuClinica.UI.Views; // Para Vistas (Ventanas)
using System.Windows.Input; // Para InputManager
using TuClinica.UI.Services;


using System.Linq; // Para .OfType<MainWindow>()

// 1. El namespace debe ser este
namespace TuClinica.UI
{
    // 2. Esta es la ÚNICA definición de la clase 'App'
    public partial class App : Application
    {
        public static IHost? AppHost { get; private set; }

        #region Métodos Estáticos para Rutas y Contraseña (Definidos UNA SOLA VEZ)

        public static string GetAppDataFolderPath()
        {
            string appDataFolder = string.Empty;
            try
            {
                appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TuClinicaPD");
                Directory.CreateDirectory(appDataFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fatal al acceder a la carpeta de datos de aplicación: {ex.Message}\nLa aplicación se cerrará.", "Error de Ruta", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(2);
            }
            return appDataFolder;
        }

        public static string GetDataFolderPath()
        {
            string dataFolder = string.Empty;
            try
            {
                dataFolder = Path.Combine(GetAppDataFolderPath(), "Data");
                Directory.CreateDirectory(dataFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fatal al acceder a la subcarpeta de datos: {ex.Message}\nLa aplicación se cerrará.", "Error de Ruta", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(3);
            }
            return dataFolder;
        }

        public static string GetDatabasePath()
        {
            string dbPath = string.Empty;
            try
            {
                dbPath = Path.Combine(GetDataFolderPath(), "DentalClinic.db"); // Asegúrate que el nombre sea correcto
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fatal al construir la ruta de la base de datos: {ex.Message}\nLa aplicación se cerrará.", "Error de Ruta", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(4);
            }
            return dbPath;
        }

        public static string GetBudgetsFolderPath()
        {
            string budgetsFolder = string.Empty;
            try
            {
                budgetsFolder = Path.Combine(GetDataFolderPath(), "presupuestos");
                Directory.CreateDirectory(budgetsFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fatal al acceder a la carpeta de presupuestos: {ex.Message}\nLa aplicación se cerrará.", "Error de Ruta", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(5);
            }
            return budgetsFolder;
        }

        // *** AÑADIDO: Ruta para Recetas PDF ***
        public static string GetPrescriptionsFolderPath()
        {
            string prescriptionsFolder = string.Empty;
            try
            {
                prescriptionsFolder = Path.Combine(GetDataFolderPath(), "recetas");
                Directory.CreateDirectory(prescriptionsFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fatal al acceder a la carpeta de recetas: {ex.Message}\nLa aplicación se cerrará.", "Error de Ruta", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(6); // Código de error diferente
            }
            return prescriptionsFolder;
        }

        private static string GetEncryptedPasswordFilePath()
        {
            string filePath = string.Empty;
            try
            {
                filePath = Path.Combine(GetDataFolderPath(), "db.key");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fatal al construir la ruta del archivo de clave: {ex.Message}\nLa aplicación se cerrará.", "Error de Ruta", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(7);
            }
            return filePath;
        }

        private static string GetOrCreateDatabasePassword()
        {
            string filePath = GetEncryptedPasswordFilePath();
            byte[] entropy = Encoding.UTF8.GetBytes("TuClinicaSalt"); // Sal para DPAPI
            string password = string.Empty;

            try
            {
                if (File.Exists(filePath))
                {
                    byte[] encryptedData = File.ReadAllBytes(filePath);
                    byte[] decryptedData = ProtectedData.Unprotect(encryptedData, entropy, DataProtectionScope.CurrentUser);
                    password = Encoding.UTF8.GetString(decryptedData);
                }
                else
                {
                    string newPassword = GenerateRandomPassword();
                    byte[] passwordBytes = Encoding.UTF8.GetBytes(newPassword);
                    byte[] encryptedData = ProtectedData.Protect(passwordBytes, entropy, DataProtectionScope.CurrentUser);
                    File.WriteAllBytes(filePath, encryptedData);
                    File.SetAttributes(filePath, FileAttributes.Hidden);
                    password = newPassword;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error crítico al gestionar la clave de la base de datos: {ex.Message}\nLa aplicación se cerrará.", "Error Clave Base de Datos", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(8);
            }
            return password;
        }

        private static string GenerateRandomPassword(int length = 32)
        {
            StringBuilder res = new StringBuilder(length);
            try
            {
                const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*()_-+=<>?";
                using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                {
                    byte[] uintBuffer = new byte[sizeof(uint)];
                    while (length-- > 0)
                    {
                        rng.GetBytes(uintBuffer);
                        uint num = BitConverter.ToUInt32(uintBuffer, 0);
                        res.Append(validChars[(int)(num % (uint)validChars.Length)]);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fatal al generar la contraseña aleatoria: {ex.Message}\nLa aplicación usará una contraseña insegura o se cerrará.", "Error Generación Clave", MessageBoxButton.OK, MessageBoxImage.Error);
                return "FallbackPassword123";
            }
            return res.ToString();
        }

        #endregion // Fin de Métodos Estáticos

        public App()
        {
            // --- Inicializar Proveedor SQLCipher (Método v1.x) ---
            try
            {
                SQLitePCL.Batteries.Init();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error crítico al inicializar SQLitePCL (SQLCipher): {ex.Message}\nLa aplicación se cerrará.",
                                "Error de Inicialización", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(10);
            }
            // --- Fin Inicialización ---

            // --- Configuración de Servicios ---
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    // Configuración General
                    services.Configure<AppSettings>(hostContext.Configuration.GetSection("ClinicSettings"));
                    services.AddSingleton(sp => sp.GetRequiredService<IOptions<AppSettings>>().Value);

                    // Base de Datos
                    services.AddDbContext<AppDbContext>(options => {
                        string dbPassword = GetOrCreateDatabasePassword();
                        options.UseSqlite($"Data Source={GetDatabasePath()};Password={dbPassword}");

                    });

                    // Repositorios
                    services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
                    services.AddScoped<IPatientRepository, PatientRepository>();
                    services.AddScoped<IUserRepository, UserRepository>();
                    services.AddScoped<ITreatmentRepository, TreatmentRepository>();
                    services.AddScoped<IBudgetRepository, BudgetRepository>();
                    services.AddScoped<IMedicationRepository, MedicationRepository>();
                    services.AddScoped<IDosageRepository, DosageRepository>();
                    services.AddScoped<IClinicalEntryRepository, ClinicalEntryRepository>();
                    services.AddScoped<IPaymentRepository, PaymentRepository>();
                    services.AddScoped<IRepository<PaymentAllocation>, Repository<PaymentAllocation>>();

                    // Servicios
                    services.AddSingleton<IValidationService, ValidationService>();
                    services.AddSingleton<IAuthService, AuthService>();
                    services.AddSingleton<ILicenseService, LicenseService>();
                    services.AddScoped<IBackupService, BackupService>();
                    services.AddScoped<IPdfService>(sp => new PdfService(
                        sp.GetRequiredService<AppSettings>(),
                        GetBudgetsFolderPath(),
                        GetPrescriptionsFolderPath(), // <-- Nueva Ruta
                        sp.GetRequiredService<IPatientRepository>()
                    ));
                    services.AddScoped<IActivityLogService, ActivityLogService>();
                    services.AddSingleton<IInactivityService, InactivityService>();
                    services.AddSingleton<IDialogService, DialogService>();
                    services.AddSingleton<IFileDialogService, FileDialogService>();
                    services.AddSingleton<ICryptoService, CryptoService>();
                    services.AddSingleton<IFileSystemService, FileSystemService>();



                    // ViewModels
                    services.AddTransient<PatientsViewModel>();
                    services.AddTransient<LoginViewModel>();
                    services.AddTransient<MainWindowViewModel>();
                    services.AddTransient<BudgetsViewModel>();
                    services.AddTransient<TreatmentsViewModel>();
                    services.AddTransient<AdminViewModel>();
                    services.AddTransient<PatientSelectionViewModel>();
                    services.AddTransient<LicenseViewModel>();
                    services.AddTransient<UserEditViewModel>();
                    services.AddTransient<PrescriptionViewModel>();

                    services.AddSingleton<PatientFileViewModel>();
                    services.AddTransient<OdontogramViewModel>();
                    services.AddSingleton<HomeViewModel>();


                    // *******************************************************************

                    // Vistas
                    services.AddSingleton<MainWindow>();
                    services.AddTransient<LoginWindow>();
                    services.AddTransient<PatientSelectionDialog>();
                    services.AddTransient<UserEditDialog>();
                    services.AddTransient<OdontogramWindow>();

                    // *** CAMBIO: Eliminada la vista obsoleta ***
                    // services.AddTransient<TreatmentPriceDialog>();

                    services.AddTransient<NewPaymentDialog>();

                    // *** CAMBIO: Añadida la nueva vista ***
                    services.AddTransient<ManualChargeDialog>();
                    services.AddTransient<OdontogramStateDialog>(); // <-- REGISTRO FASE 2

                    // 3. Añadimos la nueva Vista (UserControl)
                    // (No necesitamos registrar PatientFileView.xaml porque es un UserControl
                    // cargado por un DataTemplate en MainWindow.xaml)

                    services.AddTransient<LicenseWindow>(sp => { var vm = sp.GetRequiredService<LicenseViewModel>(); return new LicenseWindow { DataContext = vm }; });
                })
                .Build();
        }

        // --- Lógica de Arranque ---
        protected override async void OnStartup(StartupEventArgs e)
        {
            await AppHost!.StartAsync();
            base.OnStartup(e);
            try
            {
                // 1. Obtenemos el servicio que acabamos de registrar
                var inactivityService = AppHost.Services.GetRequiredService<IInactivityService>();

                // 2. Nos suscribimos al evento. Esto define QUÉ PASA cuando se agota el tiempo.
                inactivityService.OnInactivity += HandleInactivity;

                // 3. Nos enganchamos al gestor de Input de WPF.
                //    Esto detectará CUALQUIER input (ratón, teclado) en la app.
                InputManager.Current.PostProcessInput += (sender, args) =>
                {
                    // Cada vez que el usuario haga algo, reseteamos el timer.
                    inactivityService.Reset();
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al inicializar el servicio de inactividad: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            try
            {
                string dbPath = GetDatabasePath();
                // *** AÑADIDO: Crear carpeta de recetas al inicio ***
                _ = GetPrescriptionsFolderPath();
                _ = GetBudgetsFolderPath(); // (Ya estaba, pero aseguramos)

                using (var scope = AppHost.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // --- INICIO DE MODIFICACIÓN ROBUSTA ---
                    // 1. Comprobar explícitamente si hay migraciones pendientes
                    var pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToList();

                    // 2. Si hay trabajo que hacer...
                    if (pendingMigrations.Any())
                    {
                        // 3. Informar al usuario ANTES de hacer nada
                        MessageBox.Show($"Se ha detectado una nueva versión. Se actualizará la base de datos para preservar sus datos.\n\nActualizaciones pendientes:\n{string.Join("\n", pendingMigrations)}",
                                        "Actualizando Base de Datos",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Information);

                        // 4. Aplicar la migración
                        await db.Database.MigrateAsync();

                        // 5. Confirmar el éxito
                        MessageBox.Show("¡Base de datos actualizada con éxito!",
                                        "Actualización Completa",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Information);
                    }
                    // 6. Si no hay migraciones pendientes, la aplicación continúa en silencio.
                    // --- FIN DE MODIFICACIÓN ROBUSTA ---

                    if (!await db.Users.AnyAsync())
                    {
                        db.Users.Add(new User { Username = "admin", HashedPassword = BCrypt.Net.BCrypt.HashPassword("admin123"), Role = UserRole.Administrador, IsActive = true, Name = "Admin" });
                        await db.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"Error DB Init: {ex.Message}" + (ex.InnerException != null ? $"\nInner: {ex.InnerException.Message}" : "");
                if (ex.Message.Contains("SQLite Error 26")) msg += "\n\nPOSIBLE CAUSA: Contraseña/BD corrupta. Borre 'db.key' y 'DentalClinic.db'.";
                MessageBox.Show($"{msg}\n\nCerrando.", "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }

            var licenseSvc = AppHost.Services.GetRequiredService<ILicenseService>();
            if (licenseSvc.IsLicenseValid())
            {
                AppHost.Services.GetRequiredService<LoginWindow>().ShowDialog();
            }
            else
            {
                AppHost.Services.GetRequiredService<LicenseWindow>().ShowDialog();
            }
        }
        private void HandleInactivity()
        {
            // Debemos asegurarnos de que esto se ejecuta en el hilo principal de la UI
            if (Application.Current.Dispatcher.CheckAccess())
            {
                PerformLogout();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(PerformLogout);
            }
        }

        // --- AÑADE ESTE NUEVO MÉTODO A LA CLASE App ---
        /// <summary>
        /// Realiza la lógica de cierre de sesión y reinicio.
        /// </summary>
        private void PerformLogout()
        {
            // Obtenemos los servicios que necesitamos del Host
            var authService = AppHost!.Services.GetRequiredService<IAuthService>();

            // 1. Deslogueamos al usuario
            authService.Logout(); // Esto también detendrá el timer (ver Paso 5)

            // 2. Buscamos la ventana principal (MainWindow) y la cerramos
            // Usamos OfType por si hay otros diálogos abiertos.
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow != null)
            {
                // Limpiamos el DataContext antes de cerrar para evitar fugas de memoria
                mainWindow.DataContext = null;
                mainWindow.Close();
            }

            // 3. Mostramos un aviso al usuario
            MessageBox.Show("Se ha cerrado la sesión por inactividad.", "Sesión Finalizada", MessageBoxButton.OK, MessageBoxImage.Warning);

            // 4. Mostramos la ventana de Login de nuevo
            // Usamos GetRequiredService para obtener una NUEVA instancia de LoginWindow
            var loginWindow = AppHost!.Services.GetRequiredService<LoginWindow>();
            loginWindow.Show();
        }
        // --- Lógica de Cierre ---
        protected override async void OnExit(ExitEventArgs e)
        {
            if (AppHost != null)
            {
                await AppHost.StopAsync();
            }
            base.OnExit(e);
        }
    }
}
