using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;
using TuClinica.Core.Interfaces.Services;
using System.Linq;
using System.IO.Compression;

namespace TuClinica.Services.Implementation
{
    public class BackupService : IBackupService
    {
        private readonly string _sourceDataPath;
        private readonly string _dbFilePath;
        private readonly string _keyFilePath;
        private readonly IDialogService _dialogService;
        private readonly ICryptoService _cryptoService;

        public BackupService(ICryptoService cryptoService, IDialogService dialogService)
        {
            _cryptoService = cryptoService;
            _dialogService = dialogService;

            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TuClinicaPD");
            _sourceDataPath = Path.Combine(appData, "Data");
            _dbFilePath = Path.Combine(_sourceDataPath, "DentalClinic.db");
            _keyFilePath = Path.Combine(_sourceDataPath, "db.key");
        }

        public async Task CreateBackupAsync(string destinationPath, string portablePassword)
        {
            if (!File.Exists(_dbFilePath)) throw new FileNotFoundException("No se encuentra la base de datos.");

            string systemPassword = GetSystemDatabasePassword();

            string tempWorkDir = Path.Combine(Path.GetTempPath(), $"TC_BKP_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempWorkDir);

            string tempDbCopy = Path.Combine(tempWorkDir, "DentalClinic.db");
            string tempFilesPath = Path.Combine(tempWorkDir, "PatientFiles");
            string tempSettingsPath = Path.Combine(tempWorkDir, "appsettings.json");
            string rawPackagePath = Path.Combine(tempWorkDir, "RawPackage.dat");
            string compressedPackagePath = Path.Combine(tempWorkDir, "CompressedPackage.gz");

            try
            {
                await Task.Run(async () =>
                {
                    File.Copy(_dbFilePath, tempDbCopy, true);
                    RekeyDatabase(tempDbCopy, systemPassword, portablePassword);

                    if (Directory.Exists(Path.Combine(_sourceDataPath, "PatientFiles")))
                    {
                        CopyDirectory(Path.Combine(_sourceDataPath, "PatientFiles"), tempFilesPath);
                    }
                    else
                    {
                        Directory.CreateDirectory(tempFilesPath);
                    }

                    string sourceSettings = Path.Combine(_sourceDataPath, "appsettings.json");
                    if (File.Exists(sourceSettings))
                    {
                        File.Copy(sourceSettings, tempSettingsPath, true);
                    }

                    using (var fs = new FileStream(rawPackagePath, FileMode.Create))
                    using (var writer = new BinaryWriter(fs, Encoding.UTF8))
                    {
                        writer.Write("TUCLINICA_SECURE_BKP_V1");
                        writer.Write(1);

                        byte[] dbBytes = File.ReadAllBytes(tempDbCopy);
                        writer.Write(dbBytes.Length);
                        writer.Write(dbBytes);

                        var files = Directory.GetFiles(tempFilesPath, "*.*", SearchOption.AllDirectories);
                        writer.Write(files.Length);

                        foreach (string filePath in files)
                        {
                            string relativePath = Path.GetRelativePath(tempFilesPath, filePath);
                            byte[] fileBytes = File.ReadAllBytes(filePath);

                            writer.Write(relativePath);
                            writer.Write(fileBytes.Length);
                            writer.Write(fileBytes);
                        }

                        if (File.Exists(tempSettingsPath))
                        {
                            byte[] settingsBytes = File.ReadAllBytes(tempSettingsPath);
                            writer.Write(true);
                            writer.Write(settingsBytes.Length);
                            writer.Write(settingsBytes);
                        }
                        else
                        {
                            writer.Write(false);
                        }
                    }

                    using (var input = new FileStream(rawPackagePath, FileMode.Open))
                    using (var output = new FileStream(compressedPackagePath, FileMode.Create))
                    using (var gzip = new GZipStream(output, CompressionMode.Compress))
                    {
                        await input.CopyToAsync(gzip);
                    }

                    using (var fsInput = new FileStream(compressedPackagePath, FileMode.Open))
                    using (var fsOutput = new FileStream(destinationPath, FileMode.Create))
                    {
                        await _cryptoService.EncryptAsync(fsInput, fsOutput, portablePassword);
                    }
                });
            }
            catch (IOException ex)
            {
                throw new Exception($"Error de I/O al crear backup: {ex.Message}", ex);
            }
            catch (CryptographicException ex)
            {
                throw new Exception($"Error de encriptación: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Fallo crítico al crear backup: {ex.Message}", ex);
            }
            finally
            {
                if (Directory.Exists(tempWorkDir))
                {
                    try { Directory.Delete(tempWorkDir, true); } catch { }
                }
            }
        }

        public async Task RestoreBackupAsync(string sourceBackupPath, string portablePassword)
        {
            if (!File.Exists(sourceBackupPath)) throw new FileNotFoundException("No se encuentra el archivo.");

            string currentSystemPassword = GetSystemDatabasePassword();

            string tempRestoreDir = Path.Combine(Path.GetTempPath(), $"TC_RST_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempRestoreDir);

            string compressedPackagePath = Path.Combine(tempRestoreDir, "CompressedPackage.gz");
            string rawPackagePath = Path.Combine(tempRestoreDir, "RawPackage.dat");
            string restoredDbPath = Path.Combine(tempRestoreDir, "Restored.db");
            string restoredFilesPath = Path.Combine(tempRestoreDir, "PatientFiles");
            string restoredSettingsPath = Path.Combine(tempRestoreDir, "appsettings.json");

            try
            {
                await Task.Run(async () =>
                {
                    using (var fsInput = new FileStream(sourceBackupPath, FileMode.Open))
                    using (var fsOutput = new FileStream(compressedPackagePath, FileMode.Create))
                    {
                        await _cryptoService.DecryptAsync(fsInput, fsOutput, portablePassword);
                    }

                    using (var input = new FileStream(compressedPackagePath, FileMode.Open))
                    using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                    using (var output = new FileStream(rawPackagePath, FileMode.Create))
                    {
                        await gzip.CopyToAsync(output);
                    }

                    using (var fs = new FileStream(rawPackagePath, FileMode.Open))
                    using (var reader = new BinaryReader(fs, Encoding.UTF8))
                    {
                        string signature = reader.ReadString();
                        if (signature != "TUCLINICA_SECURE_BKP_V1")
                            throw new Exception("El archivo no es un backup válido.");

                        int version = reader.ReadInt32();
                        if (version != 1)
                            throw new Exception($"Versión no compatible: {version}");

                        int dbSize = reader.ReadInt32();
                        File.WriteAllBytes(restoredDbPath, reader.ReadBytes(dbSize));

                        int fileCount = reader.ReadInt32();
                        for (int i = 0; i < fileCount; i++)
                        {
                            string relativePath = reader.ReadString();
                            int fileSize = reader.ReadInt32();
                            byte[] fileContent = reader.ReadBytes(fileSize);

                            string targetPath = Path.Combine(restoredFilesPath, relativePath);
                            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);
                            File.WriteAllBytes(targetPath, fileContent);
                        }

                        bool hasSettings = reader.ReadBoolean();
                        if (hasSettings)
                        {
                            int settingsSize = reader.ReadInt32();
                            File.WriteAllBytes(restoredSettingsPath, reader.ReadBytes(settingsSize));
                        }
                    }

                    RekeyDatabase(restoredDbPath, portablePassword, currentSystemPassword);

                    PerformSafeRestore(restoredDbPath, restoredFilesPath, restoredSettingsPath);
                });
            }
            catch (CryptographicException)
            {
                throw new Exception("Contraseña incorrecta o archivo dañado.");
            }
            catch (IOException ex)
            {
                throw new Exception("Error de I/O al restaurar: " + ex.Message);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error crítico al restaurar: {ex.Message}");
            }
        }

        private void PerformSafeRestore(string newDbSource, string newFilesSource, string newSettingsSource)
        {
            string targetDb = _dbFilePath;
            string targetFiles = Path.Combine(_sourceDataPath, "PatientFiles");
            string targetSettings = Path.Combine(_sourceDataPath, "appsettings.json");

            string currentExePath = Process.GetCurrentProcess().MainModule?.FileName ?? throw new InvalidOperationException("No se pudo obtener la ruta del ejecutable.");

            string escapedDbSource = newDbSource.Replace("\"", "\\\"");
            string escapedTargetDb = targetDb.Replace("\"", "\\\"");
            string escapedFilesSource = newFilesSource.Replace("\"", "\\\"");
            string escapedTargetFiles = targetFiles.Replace("\"", "\\\"");
            string escapedSettingsSource = newSettingsSource.Replace("\"", "\\\"");
            string escapedTargetSettings = targetSettings.Replace("\"", "\\\"");

            string args = $"--restore \"{escapedDbSource}\" \"{escapedTargetDb}\" \"{escapedFilesSource}\" \"{escapedTargetFiles}\" \"{escapedSettingsSource}\" \"{escapedTargetSettings}\"";
            Process.Start(new ProcessStartInfo
            {
                FileName = currentExePath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            Environment.Exit(0);
        }

        private void RekeyDatabase(string dbPath, string oldPassword, string newPassword)
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Password = oldPassword,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false
            }.ToString();

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    string safePassword = newPassword.Replace("'", "''");
                    command.CommandText = $"PRAGMA rekey = '{safePassword}';";
                    command.ExecuteNonQuery();
                }
                connection.Close();
                SqliteConnection.ClearPool(connection);
            }
        }

        private string GetSystemDatabasePassword()
        {
            if (!File.Exists(_keyFilePath))
            {
                throw new Exception("Error de Integridad: No se encuentra db.key.");
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
                throw new Exception("No se puede leer la llave de seguridad de este PC.");
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
    }
}