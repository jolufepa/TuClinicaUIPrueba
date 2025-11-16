// En: TuClinica.Core/Interfaces/Services/ISettingsService.cs
using TuClinica.Core.Models;

namespace TuClinica.Core.Interfaces.Services
{
    /// <summary>
    /// Servicio para leer y escribir la configuración de la aplicación (appsettings.json)
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Obtiene la configuración actual de la clínica.
        /// </summary>
        AppSettings GetSettings();

        /// <summary>
        /// Guarda las modificaciones en el archivo appsettings.json.
        /// </summary>
        /// <param name="settings">El objeto de configuración a guardar.</param>
        /// <returns>True si se guardó, False si hubo un error.</returns>
        Task<bool> SaveSettingsAsync(AppSettings settings);
    }
}