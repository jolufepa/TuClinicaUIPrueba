using System.Threading.Tasks;

namespace TuClinica.Core.Interfaces.Services
{
    public interface IBackupService
    {
        /// <summary>
        /// Crea un ZIP con todos los datos y lo encripta con la contraseña proporcionada.
        /// </summary>
        Task CreateBackupAsync(string destinationPath, string password);

        /// <summary>
        /// Desencripta el backup, verifica su integridad y restaura los datos.
        /// </summary>
        Task RestoreBackupAsync(string sourceEncryptedPath, string password);
    }
}