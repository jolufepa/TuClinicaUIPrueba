using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Text;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Services.Implementation;
using System.Security.Cryptography;
using System.Diagnostics;

namespace TuClinica.Services.Tests
{
    [TestClass]
    public class LicenseServiceTests
    {
        private Mock<IFileSystemService> _fileSystemMock;

        // --- DATOS DE LICENCIA 100% VERIFICADOS ---
        // Contenido (decodificado): "MachineID=FINAL_TEST_123"
        private const string FakeLicenseData = "TWFjaGluZUlEPUZJTkFMX1RFU1RfMTIz";

        // Firma (Base64) correspondiente a "FINAL_TEST_123" y tu PublicKey
        private const string FakeSignature = "enUGb5K5KgJQ44qSfUeIpPTFWAMqwdeEsHkeHBpjSsohRcRoBbw+KFlUis1JVHee3IIlnJ0++BFmfey/gD2aKIikQZhYE6lC0kpRJef5CxpMS16y0ftppD1Jgx1oUL9GY1J6Lz9rLxItbJaSC0D1csJ7bl3DdktJS5WyLBZX1Ls2fdNmtxLJ4Je31S5j7IdlvXWmdqbZlPjAQbCuN6ApfQvbCFGBlqldQWYA+a1W3E26fH6TN80XDxuP23q/DiG3pL3IsVLV2Me1qOWW+tSGte27lnLgFen0pkyyHqoav++cRq/V7haFvXh/6vn+8ydMfsdVnWF433pecDTq6mqJGg==";

        private const string FakeLicenseFileContent = FakeLicenseData + "\n--SIGNATURE--\n" + FakeSignature;

        private Mock<LicenseService> _licenseServiceMock;

        
        [TestInitialize]
        public void Setup()
        {
            _fileSystemMock = new Mock<IFileSystemService>();

            _licenseServiceMock = new Mock<LicenseService>(_fileSystemMock.Object);

            _licenseServiceMock.CallBase = true;
        }

        [TestMethod]
        public void IsLicenseValid_DebeDevolverFalse_SiNoExisteArchivo()
        {
            // Arrange
            _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

            // Act
            bool result = _licenseServiceMock.Object.IsLicenseValid();

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsLicenseValid_DebeDevolverFalse_SiFirmaEsInvalida()
        {
            // Arrange
            _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(fs => fs.ReadAllText(It.IsAny<string>()))
                           .Returns(FakeLicenseData + "\n--SIGNATURE--\n" + "FIRMA_CORRUPTA");

            // Act
            bool result = _licenseServiceMock.Object.IsLicenseValid();

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsLicenseValid_DebeDevolverFalse_SiMachineIdEsIncorrecto()
        {
            // Arrange
            _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(fs => fs.ReadAllText(It.IsAny<string>()))
                           .Returns(FakeLicenseFileContent);

            // La licencia espera "FINAL_TEST_123", pero devolvemos "ID_INCORRECTO"
            _licenseServiceMock.Setup(s => s.GetMachineIdString()).Returns("ID_INCORRECTO");

            // Act
            bool result = _licenseServiceMock.Object.IsLicenseValid();

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsLicenseValid_DebeDevolverTrue_SiLicenciaYMachineIdSonCorrectos()
        {
            // Arrange
            _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(fs => fs.ReadAllText(It.IsAny<string>()))
                           .Returns(FakeLicenseFileContent);

            // La licencia espera "FINAL_TEST_123" y devolvemos "FINAL_TEST_123"
            _licenseServiceMock.Setup(s => s.GetMachineIdString()).Returns("FINAL_TEST_123");

            // Act
            bool result = _licenseServiceMock.Object.IsLicenseValid();

            // Assert
            Assert.IsTrue(result, "El test falló. IsLicenseValid() devolvió 'false' inesperadamente.");
        }
    }
}