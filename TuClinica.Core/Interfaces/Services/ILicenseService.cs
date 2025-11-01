namespace TuClinica.Core.Interfaces.Services
{
    public interface ILicenseService
    {
        /// <summary>
        /// Obtiene el identificador único de la máquina (Machine ID).
        /// </summary>
        /// <returns>Un string que representa el fingerprint del hardware.</returns>
        string GetMachineIdString();

        /// <summary>
        /// Comprueba si la licencia actual es válida para esta máquina.
        /// </summary>
        /// <returns>True si la licencia es válida, False en caso contrario.</returns>
        bool IsLicenseValid();

        // (Añadiremos más métodos después, como 'ActivateLicense')
    }
}