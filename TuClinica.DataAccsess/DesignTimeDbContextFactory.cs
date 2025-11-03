using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;

namespace TuClinica.DataAccess
{
    /// <summary>
    /// Esta clase le dice a las herramientas 'dotnet ef' cómo crear una instancia de AppDbContext
    /// fuera del entorno de la aplicación WPF (ej: para crear migraciones).
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        /// <summary>
        /// Un servicio de autenticación falso que no hace nada,
        /// solo para satisfacer la inyección de dependencias del constructor de AppDbContext.
        /// </summary>
        private class MockAuthService : IAuthService
        {
            public User? CurrentUser => null;
            public Task<bool> LoginAsync(string username, string password) => Task.FromResult(false);
            public void Logout() { }
        }

        public AppDbContext CreateDbContext(string[] args)
        {
            // 1. Crear el servicio falso
            IAuthService mockAuthService = new MockAuthService();

            // 2. Obtener la contraseña de la BD (replicando la lógica de app.xaml.cs)
            string dbPassword = GetOrCreateDatabasePassword();
            string dbPath = GetDatabasePath();
            string connectionString = $"Data Source={dbPath};Password={dbPassword}";

            // 3. Crear las opciones del DbContext
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlite(connectionString);

            // 4. Devolver la nueva instancia de AppDbContext
            return new AppDbContext(optionsBuilder.Options, mockAuthService);
        }

        #region Lógica de Clave/Ruta (Copiada de app.xaml.cs sin UI)

        // Esta es una copia de la lógica de app.xaml.cs, pero eliminando 
        // cualquier MessageBox o Environment.Exit para que sea compatible con la
        // herramienta de línea de comandos.

        private static string GetAppDataFolderPath()
        {
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TuClinicaPD");
            Directory.CreateDirectory(appDataFolder);
            return appDataFolder;
        }

        private static string GetDataFolderPath()
        {
            string dataFolder = Path.Combine(GetAppDataFolderPath(), "Data");
            Directory.CreateDirectory(dataFolder);
            return dataFolder;
        }

        private static string GetDatabasePath()
        {
            return Path.Combine(GetDataFolderPath(), "DentalClinic.db");
        }

        private static string GetEncryptedPasswordFilePath()
        {
            return Path.Combine(GetDataFolderPath(), "db.key");
        }

        private static string GetOrCreateDatabasePassword()
        {
            string filePath = GetEncryptedPasswordFilePath();
            byte[] entropy = Encoding.UTF8.GetBytes("TuClinicaSalt");
            string password;

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
                // La herramienta de línea de comandos verá esta excepción
                throw new Exception($"Error crítico al gestionar la clave de la base de datos para migraciones: {ex.Message}", ex);
            }
            return password;
        }

        private static string GenerateRandomPassword(int length = 32)
        {
            StringBuilder res = new StringBuilder(length);
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
            return res.ToString();
        }

        #endregion
    }
}