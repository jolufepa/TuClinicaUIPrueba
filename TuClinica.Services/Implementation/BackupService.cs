using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;
using TuClinica.Core.Interfaces.Services;

namespace TuClinica.Services.Implementation
{
    public class BackupService : IBackupService
    {
        private readonly string _sourceDataPath;
        private readonly string _dbFilePath;
        private readonly string _keyFilePath;
        private readonly IDialogService _dialogService;

        public BackupService(ICryptoService cryptoService, IDialogService dialogService)
        {
            _dialogService = dialogService;

            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TuClinicaPD");
            _sourceDataPath = Path.Combine(appData, "Data");
            _dbFilePath = Path.Combine(_sourceDataPath, "DentalClinic.db");
            _keyFilePath = Path.Combine(_sourceDataPath, "db.key");
        }

        // --------------------------------------------------------------------------------------------
        // CREAR BACKUP: SystemKey (Tu PC) -> UserPassword (Portable)
        // --------------------------------------------------------------------------------------------
        public async Task CreateBackupAsync(string destinationPath, string portablePassword)
        {
            if (!File.Exists(_dbFilePath)) throw new FileNotFoundException("No se encuentra la base de datos.");

            // 1. Leemos la clave interna de TU PC actual (db.key)
            string systemPassword = GetSystemDatabasePassword();

            await Task.Run(() =>
            {
                // 2. Copiamos la BD a un temporal para trabajar seguros
                string tempDbPath = Path.Combine(Path.GetTempPath(), $"TempBackup_{Guid.NewGuid()}.db");
                File.Copy(_dbFilePath, tempDbPath, true);

                try
                {
                    // 3. Abrimos la copia usando la contraseña del SISTEMA
                    var connectionString = new SqliteConnectionStringBuilder
                    {
                        DataSource = tempDbPath,
                        Password = systemPassword,
                        Mode = SqliteOpenMode.ReadWrite,
                        Pooling = false // INTENTO 1: Desactivar pooling para este backup
                    }.ToString();

                    using (var connection = new SqliteConnection(connectionString))
                    {
                        connection.Open();

                        // 4. REKEY: Cambiamos la llave maestra a la contraseña del usuario
                        using (var command = connection.CreateCommand())
                        {
                            string safePassword = portablePassword.Replace("'", "''");
                            command.CommandText = $"PRAGMA rekey = '{safePassword}';";
                            command.ExecuteNonQuery();
                        }

                        // --- CORRECCIÓN CRÍTICA AQUÍ ---
                        // Cerramos explícitamente y limpiamos el pool para soltar el archivo
                        connection.Close();
                        SqliteConnection.ClearPool(connection);
                        // -------------------------------
                    }

                    // Pequeña pausa de seguridad para asegurar que el SO libera el handler
                    System.Threading.Thread.Sleep(100);

                    // 5. Copiamos al destino final
                    File.Copy(tempDbPath, destinationPath, true);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error al exportar backup portable: {ex.Message}", ex);
                }
                finally
                {
                    // Intentamos borrar el temporal. Si falla por bloqueo, no rompemos la app, 
                    // el SO lo limpiará eventualmente.
                    try
                    {
                        if (File.Exists(tempDbPath)) File.Delete(tempDbPath);
                    }
                    catch { /* Ignorar error de borrado temporal */ }
                }
            });
        }

        // --------------------------------------------------------------------------------------------
        // RESTAURAR: UserPassword (Portable) -> SystemKey (Nuevo PC)
        // --------------------------------------------------------------------------------------------
        public async Task RestoreBackupAsync(string sourceBackupPath, string portablePassword)
        {
            if (!File.Exists(sourceBackupPath)) throw new FileNotFoundException("No se encuentra el archivo.");

            // 1. Obtenemos la contraseña del sistema del NUEVO PC
            string currentSystemPassword = GetSystemDatabasePassword();

            await Task.Run(() =>
            {
                // 2. Copiamos el backup a un temporal
                string tempDbPath = Path.Combine(Path.GetTempPath(), $"TempRestore_{Guid.NewGuid()}.db");
                File.Copy(sourceBackupPath, tempDbPath, true);

                try
                {
                    // 3. Abrimos el backup usando la contraseña PORTABLE
                    var connectionString = new SqliteConnectionStringBuilder
                    {
                        DataSource = tempDbPath,
                        Password = portablePassword,
                        Mode = SqliteOpenMode.ReadWrite,
                        Pooling = false // Desactivamos pooling también aquí
                    }.ToString();

                    using (var connection = new SqliteConnection(connectionString))
                    {
                        connection.Open();

                        // 4. REKEY INVERSO: Cambiamos a la contraseña del SISTEMA
                        using (var command = connection.CreateCommand())
                        {
                            string safePassword = currentSystemPassword.Replace("'", "''");
                            command.CommandText = $"PRAGMA rekey = '{safePassword}';";
                            command.ExecuteNonQuery();
                        }

                        // CORRECCIÓN CRÍTICA: Soltar archivo
                        connection.Close();
                        SqliteConnection.ClearPool(connection);
                    }

                    System.Threading.Thread.Sleep(100);

                    // 5. Reemplazo seguro
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _dialogService.ShowMessage(
                            "Copia validada y adaptada a este equipo.\n\nLa aplicación se reiniciará.",
                            "Restauración Exitosa");

                        PerformSafeRestore(tempDbPath);
                    });
                }
                catch (SqliteException ex)
                {
                    if (ex.Message.Contains("file is not a database") || ex.SqliteErrorCode == 26)
                        throw new Exception("La contraseña proporcionada no es válida para este backup.");
                    else
                        throw;
                }
            });
        }

        private void PerformSafeRestore(string newDbFileSource)
        {
            string batPath = Path.Combine(Path.GetTempPath(), "restore_rekey.bat");
            string targetFolder = _sourceDataPath.TrimEnd(Path.DirectorySeparatorChar);

            string script = $@"
@echo off
timeout /t 2 /nobreak > NUL
copy /Y ""{newDbFileSource}"" ""{targetFolder}\DentalClinic.db""
del ""{newDbFileSource}""
del ""%~f0""
";
            File.WriteAllText(batPath, script);

            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = batPath,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            }.Start();

            Application.Current.Shutdown();
        }

        private string GetSystemDatabasePassword()
        {
            if (!File.Exists(_keyFilePath))
            {
                throw new Exception("Error Crítico: No se encuentra 'db.key'. La aplicación debe iniciarse correctamente al menos una vez.");
            }

            try
            {
                byte[] entropy = Encoding.UTF8.GetBytes("TuClinicaSalt");
                byte[] encryptedData = File.ReadAllBytes(_keyFilePath);
                byte[] decryptedData = ProtectedData.Unprotect(encryptedData, entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decryptedData);
            }
            catch
            {
                throw new Exception("No se pudo leer la llave de seguridad (db.key).");
            }
        }
    }
}