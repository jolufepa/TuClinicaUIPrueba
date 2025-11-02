using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Cryptography; // Para CryptographicException
using System.Text;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Services.Implementation;

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
    }
}