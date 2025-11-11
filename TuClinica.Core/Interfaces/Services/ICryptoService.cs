// En: TuClinica.Core/Interfaces/Services/ICryptoService.cs
using System.IO; // <-- AÑADIR
using System.Threading.Tasks; // <-- AÑADIR

namespace TuClinica.Core.Interfaces.Services
{
    public interface ICryptoService
    {
        /// <summary>
        /// Encripta un array de bytes usando AES-GCM y una contraseña.
        /// </summary>
        byte[] Encrypt(byte[] dataToEncrypt, string password);

        /// <summary>
        /// Desencripta un array de bytes usando AES-GCM y una contraseña.
        /// Devuelve null si la contraseña es incorrecta o los datos están corruptos.
        /// </summary>
        byte[]? Decrypt(byte[] encryptedDataWithMeta, string password);

        // --- MÉTODOS DE STREAMING AÑADIDOS ---

        /// <summary>
        /// Encripta un stream de entrada y escribe el resultado en un stream de salida.
        /// (Implementación: AES-CBC + HMAC-SHA256)
        /// </summary>
        /// <param name="inputStream">Stream con los datos a encriptar.</param>
        /// <param name="outputStream">Stream donde se escribirán los datos encriptados.</param>
        /// <param name="password">Contraseña.</param>
        Task EncryptAsync(Stream inputStream, Stream outputStream, string password);

        /// <summary>
        /// Desencripta un stream de entrada y escribe el resultado en un stream de salida.
        /// (Implementación: AES-CBC + HMAC-SHA256)
        /// </summary>
        /// <param name="inputStream">Stream con los datos encriptados.</param>
        /// <param name="outputStream">Stream donde se escribirán los datos desencriptados.</param>
        /// <param name="password">Contraseña.</param>
        /// <exception cref="CryptographicException">Lanzada si la contraseña es incorrecta o los datos han sido manipulados.</exception>
        Task DecryptAsync(Stream inputStream, Stream outputStream, string password);
    }
}