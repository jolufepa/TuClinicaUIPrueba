// En: TuClinica.Services.Tests/CryptoServiceTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Cryptography; // Para CryptographicException
using System.Text;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Services.Implementation;
using System.IO; // <-- AÑADIDO
using System.Threading.Tasks; // <-- AÑADIDO

namespace TuClinica.Services.Tests
{
    [TestClass]
    public class CryptoServiceTests
    {
        private ICryptoService _cryptoService;
        private byte[] _testData;
        private string _testPassword;

        [TestInitialize]
        public void Setup()
        {
            _cryptoService = new CryptoService(); // ¡Sin Mocks!
            _testData = Encoding.UTF8.GetBytes("¡Esto es un backup secreto!");
            _testPassword = "MiPasswordSuperSegura123";
        }

        #region Tests AES-GCM (Métodos Antiguos)

        [TestMethod]
        public void Encrypt_Decrypt_DebeDevolverDatosOriginales_ConPasswordCorrecta()
        {
            // Arrange (Preparar)
            // (hecho en Setup)

            // Act (Actuar)
            byte[] encryptedData = _cryptoService.Encrypt(_testData, _testPassword);
            byte[] decryptedData = _cryptoService.Decrypt(encryptedData, _testPassword);

            // Assert (Verificar)
            Assert.IsNotNull(decryptedData, "La desencriptación no debió fallar.");
            // Comparamos los arrays byte a byte
            CollectionAssert.AreEqual(_testData, decryptedData, "Los datos desencriptados no coinciden con los originales.");
        }

        [TestMethod]
        public void Decrypt_DebeDevolverNull_ConPasswordIncorrecta()
        {
            // Arrange
            byte[] encryptedData = _cryptoService.Encrypt(_testData, _testPassword);
            string wrongPassword = "PasswordIncorrecta";

            // Act
            byte[] decryptedData = _cryptoService.Decrypt(encryptedData, wrongPassword);

            // Assert
            Assert.IsNull(decryptedData, "La desencriptación debió devolver null con una contraseña incorrecta.");
        }

        [TestMethod]
        public void Decrypt_DebeDevolverNull_ConDatosCorruptos()
        {
            // Arrange
            byte[] encryptedData = _cryptoService.Encrypt(_testData, _testPassword);

            // Corrompemos los datos (cambiamos el último byte)
            encryptedData[encryptedData.Length - 1] = (byte)(encryptedData[encryptedData.Length - 1] + 1);

            // Act
            byte[] decryptedData = _cryptoService.Decrypt(encryptedData, _testPassword);

            // Assert
            Assert.IsNull(decryptedData, "La desencriptación debió devolver null para datos corruptos (fallo de Tag).");
        }

        #endregion

        #region Tests AES-CBC + HMAC (Nuevos Métodos de Streaming)

        [TestMethod]
        public async Task EncryptAsync_DecryptAsync_Stream_DebeDevolverDatosOriginales()
        {
            // Arrange
            await using var inputStream = new MemoryStream(_testData);
            await using var encryptedStream = new MemoryStream();
            await using var outputStream = new MemoryStream();

            // Act
            // 1. Encriptar (Input -> Encrypted)
            await _cryptoService.EncryptAsync(inputStream, encryptedStream, _testPassword);

            // Rebobinar el stream encriptado para leerlo
            encryptedStream.Seek(0, SeekOrigin.Begin);

            // 2. Desencriptar (Encrypted -> Output)
            await _cryptoService.DecryptAsync(encryptedStream, outputStream, _testPassword);

            // 3. Obtener los bytes resultantes
            byte[] decryptedData = outputStream.ToArray();

            // Assert
            Assert.IsNotNull(decryptedData);
            CollectionAssert.AreEqual(_testData, decryptedData, "Los datos desencriptados por stream no coinciden.");
        }

        [TestMethod]
        public async Task DecryptAsync_Stream_DebeLanzarExcepcion_ConPasswordIncorrecta()
        {
            // Arrange
            await using var inputStream = new MemoryStream(_testData);
            await using var encryptedStream = new MemoryStream();
            await using var outputStream = new MemoryStream();

            // 1. Encriptar con contraseña buena
            await _cryptoService.EncryptAsync(inputStream, encryptedStream, _testPassword);
            encryptedStream.Seek(0, SeekOrigin.Begin);

            string wrongPassword = "PasswordIncorrecta";

            // Act & Assert
            // 2. Intentar desencriptar con contraseña mala
            // Usamos Assert.ThrowsExceptionAsync para verificar que se lanza la excepción
            await Assert.ThrowsExceptionAsync<CryptographicException>(async () =>
            {
                await _cryptoService.DecryptAsync(encryptedStream, outputStream, wrongPassword);
            }, "DecryptAsync debió lanzar CryptographicException con contraseña incorrecta.");
        }

        [TestMethod]
        public async Task DecryptAsync_Stream_DebeLanzarExcepcion_ConDatosCorruptos()
        {
            // Arrange
            await using var inputStream = new MemoryStream(_testData);
            await using var encryptedStream = new MemoryStream();
            await using var outputStream = new MemoryStream();

            // 1. Encriptar
            await _cryptoService.EncryptAsync(inputStream, encryptedStream, _testPassword);

            // 2. Corromper los datos (cambiamos un byte en medio del ciphertext)
            byte[] encryptedBytes = encryptedStream.ToArray();
            int midIndex = encryptedBytes.Length / 2;
            encryptedBytes[midIndex] = (byte)(encryptedBytes[midIndex] + 1);

            await using var corruptedStream = new MemoryStream(encryptedBytes);

            // Act & Assert
            // 3. Intentar desencriptar
            await Assert.ThrowsExceptionAsync<CryptographicException>(async () =>
            {
                await _cryptoService.DecryptAsync(corruptedStream, outputStream, _testPassword);
            }, "DecryptAsync debió lanzar CryptographicException por datos corruptos (fallo de HMAC).");
        }

        #endregion
    }
}