using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics; // Para Process.Start
using System.IO; // Para File
using System.Threading.Tasks;
using System.Windows;
using TuClinica.Core.Interfaces.Services;
using Microsoft.Win32; // *** AÑADIR ESTE USING ***

namespace TuClinica.UI.ViewModels
{
    public partial class LicenseViewModel : BaseViewModel
    {
        private readonly ILicenseService _licenseService;
        private readonly string _targetLicensePath;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private string _machineId = "Cargando...";

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _closeWindowFlag = false;

        public LicenseViewModel(ILicenseService licenseService, IDialogService dialogService)
        {
            _licenseService = licenseService;
            _dialogService = dialogService; // <-- AÑADIR ESTA LÍNEA
            LoadMachineId();
            // Guardamos la ruta donde la app buscará la licencia
            _targetLicensePath = Path.Combine(AppContext.BaseDirectory, "license.dat");
        }

        private void LoadMachineId()
        {
            MachineId = _licenseService.GetMachineIdString();
        }

        [RelayCommand]
        private void CopyMachineId()
        {
            try
            {
                Clipboard.SetText(MachineId);
                _dialogService.ShowMessage("Machine ID copiado al portapapeles.", "Copiado");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"No se pudo copiar al portapapeles: {ex.Message}";
            }
        }

        // *** MÉTODO ImportLicense() ACTUALIZADO ***
        [RelayCommand]
        private void ImportLicense()
        {
            ErrorMessage = string.Empty; // Limpiar errores previos

            // 1. Mostrar diálogo para seleccionar archivo .dat
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Archivos de Licencia (*.dat)|*.dat",
                Title = "Seleccionar archivo de licencia"
            };

            // 2. Si el usuario selecciona un archivo
            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;

                try
                {
                    // 3. Copiar el archivo seleccionado a la carpeta de la aplicación,
                    //    renombrándolo a "license.dat" y sobrescribiendo si ya existe.
                    File.Copy(selectedFilePath, _targetLicensePath, true);

                    // 4. Verificar si AHORA la licencia es válida
                    if (_licenseService.IsLicenseValid())
                    {
                        _dialogService.ShowMessage("¡Licencia importada y activada correctamente!\n\nLa aplicación se reiniciará.", "Activación Exitosa");

                        // --- CORRECCIÓN ---
                        // Usamos Environment.ProcessPath, que es 100% fiable
                        // para obtener la ruta al .exe actual, incluso en ClickOnce.
                        string? exePath = Environment.ProcessPath;

                        if (!string.IsNullOrEmpty(exePath))
                        {
                            Process.Start(exePath); // Inicia una nueva instancia
                            Application.Current.Shutdown(); // Cierra esta instancia
                        }
                        else
                        {
                            // Fallback por si algo muy raro pasa
                            ErrorMessage = "Error al reiniciar. Por favor, cierre y abra la aplicación manualmente.";
                        }
                    }
                    else
                    {
                        ErrorMessage = "La licencia importada no es válida para este equipo.";
                    }
                }
                catch (IOException ioEx) // Error al copiar (ej. permisos)
                {
                    ErrorMessage = $"Error al copiar el archivo de licencia:\n{ioEx.Message}";
                }
                catch (UnauthorizedAccessException uaEx) // Error de permisos
                {
                    ErrorMessage = $"Error de permisos al copiar el archivo:\n{uaEx.Message}";
                }
                catch (Exception ex) // Otros errores
                {
                    ErrorMessage = $"Error inesperado al importar la licencia:\n{ex.Message}";
                }
            }
            // Si el usuario cancela el diálogo, no hacemos nada.
        }


        [RelayCommand]
        private void CloseWindow(Window window)
        {
            window?.Close();
            Application.Current.Shutdown();
        }
    }
}