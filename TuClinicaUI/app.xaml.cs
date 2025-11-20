using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using TuClinica.Core.Enums;
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

        #region Métodos Estáticos (Rutas y Claves)
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
        #endregion

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
                    services.AddScoped<ITreatmentPlanItemRepository, TreatmentPlanItemRepository>();
                    services.AddScoped<IRepository<TreatmentPackItem>, Repository<TreatmentPackItem>>();
                    services.AddScoped<IPatientAlertRepository, PatientAlertRepository>();
                    services.AddScoped<IRepository<LinkedDocument>, Repository<LinkedDocument>>();

                    // Servicios
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
                    services.AddTransient<OdontogramViewModel>();
                    services.AddSingleton<HomeViewModel>();
                    services.AddTransient<FinancialSummaryViewModel>();

                    // Sub-ViewModels
                    services.AddTransient<PatientInfoViewModel>();
                    
                    services.AddTransient<PatientDocumentsViewModel>();
                    services.AddTransient<PatientAlertsViewModel>();
                    services.AddTransient<PatientFinancialViewModel>();
                    services.AddTransient<PatientTreatmentPlanViewModel>();

                    // ViewModel Padre (Conductor) - INYECCIÓN ACTUALIZADA
                    services.AddSingleton<PatientFileViewModel>(sp =>
                        new PatientFileViewModel(
                            sp.GetRequiredService<IAuthService>(),
                            sp.GetRequiredService<IDialogService>(),
                            sp.GetRequiredService<IServiceScopeFactory>(),
                            sp.GetRequiredService<IFileDialogService>(),
                            sp.GetRequiredService<IPdfService>(),
                            // Hijos
                            sp.GetRequiredService<PatientInfoViewModel>(),
                            sp.GetRequiredService<PatientDocumentsViewModel>(),
                            sp.GetRequiredService<PatientAlertsViewModel>(),
                            sp.GetRequiredService<PatientFinancialViewModel>(),
                            sp.GetRequiredService<PatientTreatmentPlanViewModel>()
                        ));

                    // Vistas
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

        protected override async void OnStartup(StartupEventArgs e)
        {
            await AppHost!.StartAsync();
            base.OnStartup(e);
            try { AppHost.Services.GetRequiredService<IInactivityService>().OnInactivity += PerformLogout; } catch { }

            // Init básico y Login
            try
            {
                using (var scope = AppHost.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    if (!db.Users.Any()) { db.Users.Add(new User { Username = "admin", HashedPassword = BCrypt.Net.BCrypt.HashPassword("admin123"), Role = UserRole.Administrador, IsActive = true, Name = "Admin" }); db.SaveChanges(); }
                }
            }
            catch { }

            var licenseSvc = AppHost.Services.GetRequiredService<ILicenseService>();
            if (licenseSvc.IsLicenseValid()) AppHost.Services.GetRequiredService<LoginWindow>().ShowDialog();
            else AppHost.Services.GetRequiredService<LicenseWindow>().ShowDialog();
        }

        private void PerformLogout()
        {
            var auth = AppHost!.Services.GetRequiredService<IAuthService>();
            auth.Logout();
            Application.Current.Windows.OfType<MainWindow>().FirstOrDefault()?.Close();
            AppHost!.Services.GetRequiredService<LoginWindow>().Show();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (AppHost != null) await AppHost.StopAsync();
            base.OnExit(e);
        }
    }
}