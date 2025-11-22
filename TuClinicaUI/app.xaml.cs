using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using TuClinica.Core.Interfaces;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.DataAccess;
using TuClinica.DataAccess.Repositories;
using TuClinica.Services.Implementation;
using TuClinica.UI.Services;
using TuClinica.UI.ViewModels;
using TuClinica.UI.Views;

namespace TuClinica.UI
{
    public partial class App : Application
    {
        public static IHost? AppHost { get; private set; }

        public static string GetAppDataFolderPath()
        {
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TuClinicaPD");
            Directory.CreateDirectory(appDataFolder);
            return appDataFolder;
        }
        public static string GetDataFolderPath()
        {
            string dataFolder = Path.Combine(GetAppDataFolderPath(), "Data");
            Directory.CreateDirectory(dataFolder);
            return dataFolder;
        }
        public static string GetDatabasePath() => Path.Combine(GetDataFolderPath(), "DentalClinic.db");
        public static string GetBudgetsFolderPath()
        {
            string folder = Path.Combine(GetDataFolderPath(), "presupuestos");
            Directory.CreateDirectory(folder);
            return folder;
        }
        public static string GetPrescriptionsFolderPath()
        {
            string folder = Path.Combine(GetDataFolderPath(), "recetas");
            Directory.CreateDirectory(folder);
            return folder;
        }
        private static string GetEncryptedPasswordFilePath() => Path.Combine(GetDataFolderPath(), "db.key");

        private static string GetOrCreateDatabasePassword()
        {
            string filePath = GetEncryptedPasswordFilePath();
            byte[] entropy = Encoding.UTF8.GetBytes("TuClinicaSalt");
            if (File.Exists(filePath))
            {
                byte[] encryptedData = File.ReadAllBytes(filePath);
                byte[] decryptedData = ProtectedData.Unprotect(encryptedData, entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decryptedData);
            }
            else
            {
                string newPassword = Guid.NewGuid().ToString();
                byte[] passwordBytes = Encoding.UTF8.GetBytes(newPassword);
                byte[] encryptedData = ProtectedData.Protect(passwordBytes, entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(filePath, encryptedData);
                File.SetAttributes(filePath, FileAttributes.Hidden);
                return newPassword;
            }
        }

        public App()
        {
            try { SQLitePCL.Batteries.Init(); } catch { }

            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<ISettingsService, SettingsService>();

                    services.AddDbContext<AppDbContext>(options => {
                        string dbPassword = GetOrCreateDatabasePassword();
                        options.UseSqlite($"Data Source={GetDatabasePath()};Password={dbPassword}");
                    });

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
                    services.AddScoped<ITreatmentPlanItemRepository, TreatmentPlanItemRepository>();
                    services.AddScoped<IRepository<TreatmentPackItem>, Repository<TreatmentPackItem>>();
                    services.AddScoped<IPatientAlertRepository, PatientAlertRepository>();
                    services.AddScoped<IRepository<LinkedDocument>, Repository<LinkedDocument>>();
                    services.AddScoped<IRepository<PatientFile>, Repository<PatientFile>>();

                    services.AddSingleton<IValidationService, ValidationService>();
                    services.AddSingleton<IAuthService, AuthService>();
                    services.AddSingleton<ILicenseService, LicenseService>();
                    services.AddScoped<IBackupService, BackupService>();
                    services.AddScoped<IPdfService>(sp => new PdfService(
                        sp.GetRequiredService<ISettingsService>().GetSettings(),
                        GetBudgetsFolderPath(),
                        GetPrescriptionsFolderPath(),
                        sp.GetRequiredService<IPatientRepository>()
                    ));
                    services.AddScoped<IActivityLogService, ActivityLogService>();
                    services.AddSingleton<IInactivityService, TuClinica.UI.Services.InactivityService>();
                    services.AddSingleton<IDialogService, DialogService>();
                    services.AddSingleton<IFileDialogService, FileDialogService>();
                    services.AddSingleton<ICryptoService, CryptoService>();
                    services.AddSingleton<IFileSystemService, FileSystemService>();
                    services.AddScoped<IFileStorageService, FileStorageService>();

                    services.AddTransient<TimeSelectionDialog>();

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
                    services.AddTransient<OdontogramViewModel>();
                    services.AddSingleton<HomeViewModel>();
                    services.AddTransient<FinancialSummaryViewModel>();

                    services.AddTransient<PatientInfoViewModel>();
                    services.AddTransient<PatientDocumentsViewModel>();
                    services.AddTransient<PatientAlertsViewModel>();
                    services.AddTransient<PatientFinancialViewModel>();
                    services.AddTransient<PatientTreatmentPlanViewModel>();
                    services.AddTransient<PatientOdontogramViewModel>();

                    services.AddSingleton<PatientFileViewModel>(sp =>
                        new PatientFileViewModel(
                            sp.GetRequiredService<IAuthService>(),
                            sp.GetRequiredService<IDialogService>(),
                            sp.GetRequiredService<IServiceScopeFactory>(),
                            sp.GetRequiredService<IFileDialogService>(),
                            sp.GetRequiredService<IPdfService>(),
                            sp.GetRequiredService<PatientInfoViewModel>(),
                            sp.GetRequiredService<PatientDocumentsViewModel>(),
                            sp.GetRequiredService<PatientAlertsViewModel>(),
                            sp.GetRequiredService<PatientFinancialViewModel>(),
                            sp.GetRequiredService<PatientTreatmentPlanViewModel>()
                        ));

                    services.AddSingleton<MainWindow>();
                    services.AddTransient<LoginWindow>();
                    services.AddTransient<PatientSelectionDialog>();
                    services.AddTransient<UserEditDialog>();
                    services.AddTransient<OdontogramWindow>();
                    services.AddTransient<NewPaymentDialog>();
                    services.AddTransient<ManualChargeDialog>();
                    services.AddTransient<OdontogramStateDialog>();
                    services.AddTransient<LicenseWindow>(sp => { var vm = sp.GetRequiredService<LicenseViewModel>(); return new LicenseWindow { DataContext = vm }; });
                    services.AddTransient<LinkedDocumentDialog>();
                })
                .Build();
        }

        private void PerformSafeRestore(string[] args)
        {
            if (args.Length < 7) return;

            string sourceDb = args[1];
            string targetDb = args[2];
            string sourceFiles = args[3];
            string targetFiles = args[4];
            string sourceSettings = args[5];
            string targetSettings = args[6];

            try
            {
                Thread.Sleep(2000);

                if (File.Exists(sourceDb)) File.Copy(sourceDb, targetDb, true);

                if (Directory.Exists(sourceFiles))
                {
                    if (!Directory.Exists(targetFiles)) Directory.CreateDirectory(targetFiles);
                    foreach (var file in Directory.GetFiles(targetFiles, "*.*", SearchOption.AllDirectories))
                        File.Delete(file);
                    foreach (var dir in Directory.GetDirectories(targetFiles, "*", SearchOption.AllDirectories))
                        Directory.Delete(dir, true);
                    CopyDirectory(sourceFiles, targetFiles);
                }

                if (File.Exists(sourceSettings))
                {
                    File.Copy(sourceSettings, targetSettings, true);
                }

                string tempDir = Path.GetDirectoryName(sourceDb) ?? string.Empty;
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Error de I/O al copiar: {ex.Message}", "Restauración Fallida", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error crítico: {ex.Message}", "Restauración Fallida", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? throw new InvalidOperationException();
                Process.Start(new ProcessStartInfo { FileName = exePath, UseShellExecute = false });
            }
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Length > 0 && e.Args[0] == "--restore")
            {
                PerformSafeRestore(e.Args);
                return;
            }

            base.OnStartup(e);

            try
            {
                await AppHost!.StartAsync();

                using (var scope = AppHost.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var initializer = new DatabaseInitializer(db);
                    initializer.Initialize();
                }

                try { AppHost.Services.GetRequiredService<IInactivityService>().OnInactivity += PerformLogout; } catch { }
                var licenseSvc = AppHost.Services.GetRequiredService<ILicenseService>();

                if (licenseSvc.IsLicenseValid())
                {
                    AppHost.Services.GetRequiredService<LoginWindow>().Show();
                }
                else
                {
                    AppHost.Services.GetRequiredService<LicenseWindow>().ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ERROR CRÍTICO EN EL ARRANQUE:\n\n{ex.Message}\n\nDetalle: {ex.InnerException?.Message}",
                                "Fallo al Iniciar TuClinica", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) return;
            Directory.CreateDirectory(destinationDir);
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }

        private void PerformLogout()
        {
            try
            {
                var auth = AppHost!.Services.GetRequiredService<IAuthService>();
                auth.Logout();
                Application.Current.Windows.OfType<MainWindow>().FirstOrDefault()?.Close();
                AppHost!.Services.GetRequiredService<LoginWindow>().Show();
            }
            catch { }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (AppHost != null) await AppHost.StopAsync();
            base.OnExit(e);
        }
    }
}