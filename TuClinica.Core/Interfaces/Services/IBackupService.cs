using System.Threading.Tasks;

namespace TuClinica.Core.Interfaces.Services
{
    public interface IBackupService
    {
        /// <summary>
        /// Exporta todos los datos relevantes de la aplicación a un archivo encriptado.
        /// </summary>
        /// <param name="filePath">Ruta completa donde guardar el archivo de copia.</param>
        /// <param name="password">Contraseña para encriptar la copia.</param>
        /// <returns>True si la exportación fue exitosa, False en caso contrario.</returns>
        Task<bool> ExportBackupAsync(string filePath, string password);

        /// <summary>
        /// Importa datos desde un archivo de copia encriptado, REEMPLAZANDO los datos actuales.
        /// </summary>
        /// <param name="filePath">Ruta completa del archivo de copia a importar.</param>
        /// <param name="password">Contraseña para desencriptar la copia.</param>
        /// <returns>True si la importación fue exitosa, False en caso contrario.</returns>
        Task<bool> ImportBackupAsync(string filePath, string password);
    }
}