// En: TuClinica.Services/Implementation/SettingsService.cs
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;

namespace TuClinica.Services.Implementation
{
    public class SettingsService : ISettingsService
    {
        // Ruta al archivo appsettings.json en el directorio de la aplicación
        private readonly string _settingsFilePath;
        // Caché en memoria de la configuración
        private AppSettings _cachedSettings;
        // Lock para evitar escrituras simultáneas
        private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

        public SettingsService()
        {
            _settingsFilePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            _cachedSettings = LoadSettingsFromFile();
        }

        private AppSettings LoadSettingsFromFile()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    // Si no existe, crea uno por defecto
                    var defaultSettings = new { ClinicSettings = new AppSettings() };
                    var defaultJson = JsonSerializer.Serialize(defaultSettings, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_settingsFilePath, defaultJson);
                }

                var json = File.ReadAllText(_settingsFilePath);
                // Deserializa la estructura anidada { "ClinicSettings": { ... } }
                var root = JsonSerializer.Deserialize<JsonElement>(json);
                if (root.TryGetProperty("ClinicSettings", out var settingsElement))
                {
                    var settings = JsonSerializer.Deserialize<AppSettings>(settingsElement.GetRawText());
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception)
            {
                // Error al leer/deserializar, devolvemos valores por defecto
            }
            return new AppSettings();
        }

        /// <summary>
        /// Obtiene la configuración (desde la caché en memoria).
        /// </summary>
        public AppSettings GetSettings()
        {
            return _cachedSettings;
        }

        /// <summary>
        /// Guarda la configuración en el archivo appsettings.json.
        /// </summary>
        public async Task<bool> SaveSettingsAsync(AppSettings settings)
        {
            // Espera a que el lock esté disponible
            await _fileLock.WaitAsync();
            try
            {
                // Prepara la estructura raíz { "ClinicSettings": { ... } }
                var rootObject = new
                {
                    ClinicSettings = settings
                };

                // Opciones para una escritura "bonita" (indentada)
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    // Ignorar propiedades nulas si las hubiera
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                // Serializa a JSON
                var json = JsonSerializer.Serialize(rootObject, options);

                // Escribe en el archivo de forma asíncrona
                await File.WriteAllTextAsync(_settingsFilePath, json);

                // Actualiza la caché en memoria
                _cachedSettings = settings;
                return true;
            }
            catch (Exception)
            {
                // Manejar error (ej. log)
                return false;
            }
            finally
            {
                // Libera el lock
                _fileLock.Release();
            }
        }
    }
}