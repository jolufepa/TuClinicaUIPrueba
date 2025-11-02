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
    }
}
