using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Services;

namespace TuClinica.Services.Implementation
{
    public class FileStorageService : IFileStorageService
    {
        private readonly string _baseStoragePath;

        public FileStorageService()
        {
            // Usamos la misma carpeta base que en App.xaml.cs
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TuClinicaPD");
            _baseStoragePath = Path.Combine(appData, "Data", "PatientFiles");
            Directory.CreateDirectory(_baseStoragePath);
        }

        public async Task<string> SaveFileAsync(string sourceFilePath, int patientId)
        {
            if (!File.Exists(sourceFilePath)) throw new FileNotFoundException("El archivo origen no existe.");

            // Crear carpeta por paciente
            string patientFolder = Path.Combine(_baseStoragePath, patientId.ToString());
            Directory.CreateDirectory(patientFolder);

            // Generar nombre único para evitar sobrescrituras
            string extension = Path.GetExtension(sourceFilePath);
            string newFileName = $"{Guid.NewGuid()}{extension}";
            string destPath = Path.Combine(patientFolder, newFileName);

            // Copiar archivo
            await Task.Run(() => File.Copy(sourceFilePath, destPath));

            // Retornar ruta relativa (ej: "15/asdf-1234.pdf")
            return Path.Combine(patientId.ToString(), newFileName);
        }

        public void OpenFile(string relativePath)
        {
            string fullPath = GetFullPath(relativePath);
            if (File.Exists(fullPath))
            {
                // Abrir con el visor predeterminado de Windows
                new Process
                {
                    StartInfo = new ProcessStartInfo(fullPath) { UseShellExecute = true }
                }.Start();
            }
            else
            {
                throw new FileNotFoundException("El archivo no se encuentra en el disco.", fullPath);
            }
        }

        public void DeleteFile(string relativePath)
        {
            string fullPath = GetFullPath(relativePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        public string GetFullPath(string relativePath)
        {
            return Path.Combine(_baseStoragePath, relativePath);
        }
    }
}