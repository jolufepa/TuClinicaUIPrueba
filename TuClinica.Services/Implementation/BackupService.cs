using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows; // Para Application.Current
using TuClinica.Core.Interfaces.Services;

namespace TuClinica.Services.Implementation
{
    public class BackupService : IBackupService
    {
        private readonly string _sourceDataPath;
        private readonly ICryptoService _cryptoService;
        private readonly IDialogService _dialogService;

        public BackupService(ICryptoService cryptoService, IDialogService dialogService)
        {
            _cryptoService = cryptoService;
            _dialogService = dialogService;
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TuClinicaPD");
            _sourceDataPath = Path.Combine(appData, "Data");
        }

        public async Task CreateBackupAsync(string destinationPath, string password)
        {
            string? directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string tempFolder = Path.Combine(Path.GetTempPath(), $"TuClinicaBackupTemp_{Guid.NewGuid()}");
            string tempZipPath = Path.Combine(Path.GetTempPath(), $"TuClinicaIntermediate_{Guid.NewGuid()}.zip");

            await Task.Run(async () =>
            {
                try
                {
                    if (File.Exists(destinationPath)) File.Delete(destinationPath);

                    CopyDirectory(_sourceDataPath, tempFolder);
                    ZipFile.CreateFromDirectory(tempFolder, tempZipPath, CompressionLevel.Optimal, false);

                    await using (var sourceStream = new FileStream(tempZipPath, FileMode.Open, FileAccess.Read))
                    await using (var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                    {
                        await _cryptoService.EncryptAsync(sourceStream, destStream, password);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error de seguridad al crear backup: {ex.Message}", ex);
                }
                finally
                {
                    if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
                    if (File.Exists(tempZipPath)) File.Delete(tempZipPath);
                }
            });
        }

        public async Task RestoreBackupAsync(string sourceEncryptedPath, string password)
        {
            if (!File.Exists(sourceEncryptedPath)) throw new FileNotFoundException("No se encuentra el archivo de backup.");

            await Task.Run(async () =>
            {
                string tempZipPath = Path.Combine(Path.GetTempPath(), $"TuClinicaRestore_{Guid.NewGuid()}.zip");
                string tempExtractFolder = Path.Combine(Path.GetTempPath(), $"TuClinicaRestore_{Guid.NewGuid()}");

                try
                {
                    // 1. Desencriptar
                    await using (var sourceStream = new FileStream(sourceEncryptedPath, FileMode.Open, FileAccess.Read))
                    await using (var destStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write))
                    {
                        await _cryptoService.DecryptAsync(sourceStream, destStream, password);
                    }

                    // 2. Extraer
                    Directory.CreateDirectory(tempExtractFolder);
                    ZipFile.ExtractToDirectory(tempZipPath, tempExtractFolder);

                    // 3. Validar
                    if (!File.Exists(Path.Combine(tempExtractFolder, "DentalClinic.db")) &&
                        !Directory.Exists(Path.Combine(tempExtractFolder, "PatientFiles")))
                    {
                        throw new Exception("El backup desencriptado no contiene datos válidos.");
                    }

                    // 4. Ejecutar restauración (SIN INTENTAR REINICIAR AUTOMÁTICAMENTE)
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // MENSAJE ACTUALIZADO PARA EL USUARIO
                        _dialogService.ShowMessage(
                            "Copia de seguridad verificada correctamente.\n\n" +
                            "La aplicación se cerrará ahora para aplicar los cambios.\n\n" +
                            "IMPORTANTE: Cuando se cierre, espere 5 segundos y vuelva a abrirla manualmente.",
                            "Restauración Exitosa");

                        PerformSafeRestore(tempExtractFolder);
                    });
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    throw new Exception("Contraseña incorrecta o archivo dañado.");
                }
                catch (Exception)
                {
                    if (Directory.Exists(tempExtractFolder)) Directory.Delete(tempExtractFolder, true);
                    throw;
                }
                finally
                {
                    if (File.Exists(tempZipPath)) File.Delete(tempZipPath);
                }
            });
        }

        private void PerformSafeRestore(string newContentPath)
        {
            string batPath = Path.Combine(Path.GetTempPath(), "restore_tuclinica.bat");

            // Obtenemos la ruta de datos sin barra final
            string dataDir = _sourceDataPath.TrimEnd(Path.DirectorySeparatorChar);

            // --- CAMBIO CLAVE: Eliminada la línea 'start ...' ---
            // El script solo copia los archivos y borra el temporal. No intenta abrir el .exe
            string script = $@"
@echo off
timeout /t 2 /nobreak > NUL
rmdir /S /Q ""{dataDir}""
mkdir ""{dataDir}""
xcopy ""{newContentPath}\*.*"" ""{dataDir}\"" /E /H /C /I /Y
rmdir /S /Q ""{newContentPath}""
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

            // Cierra la aplicación limpiamente
            Application.Current.Shutdown();
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
            {
                Directory.CreateDirectory(destinationDir);
                return;
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }
    }
}