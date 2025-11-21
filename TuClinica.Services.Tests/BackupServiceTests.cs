using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Services.Implementation;

namespace TuClinica.Services.Tests
{
    [TestClass]
    public class BackupServiceTests
    {
        [TestMethod]
        public void BackupService_SePuedeInstanciar()
        {
            // Mock de dependencias
            var cryptoMock = new Mock<ICryptoService>();
            var dialogMock = new Mock<IDialogService>();

            // Instanciar con ambos mocks
            var service = new BackupService(cryptoMock.Object, dialogMock.Object);

            Assert.IsNotNull(service);
        }
    }
}