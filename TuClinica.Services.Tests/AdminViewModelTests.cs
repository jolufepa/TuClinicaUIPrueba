using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.UI.ViewModels;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;

namespace TuClinica.Services.Tests
{
    [TestClass]
    public class AdminViewModelTests
    {
        // --- Mocks ---
        private Mock<IUserRepository> _userRepoMock;
        private Mock<IServiceProvider> _serviceProviderMock;
        private Mock<IBackupService> _backupServiceMock;
        private Mock<IRepository<ActivityLog>> _logRepoMock;
        private Mock<IActivityLogService> _activityLogServiceMock;
        private Mock<IDialogService> _dialogServiceMock;
        private Mock<IFileDialogService> _fileDialogServiceMock;

        // --- SUT ---
        private AdminViewModel _viewModel;

        [TestInitialize]
        public void Setup()
        {
            _userRepoMock = new Mock<IUserRepository>();
            _serviceProviderMock = new Mock<IServiceProvider>();
            _backupServiceMock = new Mock<IBackupService>();
            _logRepoMock = new Mock<IRepository<ActivityLog>>();
            _activityLogServiceMock = new Mock<IActivityLogService>();
            _dialogServiceMock = new Mock<IDialogService>();
            _fileDialogServiceMock = new Mock<IFileDialogService>();

            // Configuramos LoadLogsAsync para que no falle
            _logRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ActivityLog>());

            _viewModel = new AdminViewModel(
                _userRepoMock.Object,
                _serviceProviderMock.Object,
                _backupServiceMock.Object,
                _logRepoMock.Object,
                _activityLogServiceMock.Object,
                _dialogServiceMock.Object,
                _fileDialogServiceMock.Object
            );
        }

        [TestMethod]
        public async Task PurgeOldLogsAsync_DebeLlamarAlServicio_SiUsuarioConfirma()
        {
            // Arrange
            // 1. Simulamos que el usuario presiona "Sí"
            _dialogServiceMock
                .Setup(d => d.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(CoreDialogResult.Yes);

            // 2. Definimos la fecha de retención (2 años)
            var expectedDate = DateTime.UtcNow.AddYears(-2);

            // Act
            await _viewModel.PurgeOldLogsCommand.ExecuteAsync(null);

            // Assert
            // 3. Verificamos que el servicio fue llamado con la fecha correcta
            //    (Usamos It.Is<> para comprobar que la fecha esté muy cerca de la esperada)
            _activityLogServiceMock.Verify(
                s => s.PurgeOldLogsAsync(It.Is<DateTime>(d => (d - expectedDate).TotalSeconds < 1)),
                Times.Once,
                "No se llamó a PurgeOldLogsAsync con la fecha de retención correcta.");
        }

        [TestMethod]
        public async Task PurgeOldLogsAsync_NoDebeLlamarAlServicio_SiUsuarioCancela()
        {
            // Arrange
            // 1. Simulamos que el usuario presiona "No"
            _dialogServiceMock
                .Setup(d => d.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(CoreDialogResult.No);

            // Act
            await _viewModel.PurgeOldLogsCommand.ExecuteAsync(null);

            // Assert
            // 2. Verificamos que el servicio NUNCA fue llamado
            _activityLogServiceMock.Verify(
                s => s.PurgeOldLogsAsync(It.IsAny<DateTime>()),
                Times.Never,
                "Se llamó a PurgeOldLogsAsync aunque el usuario canceló.");
        }

        [TestMethod]
        public async Task ExportBackupAsync_DebeLlamarBackupService_SiTodosLosDialogosSonExitosos()
        {
            // Arrange
            string fakePassword = "123";
            string fakeFilePath = "C:\\test.bak";

            // 1. Simulamos la contraseña
            _dialogServiceMock
                .Setup(d => d.ShowPasswordPrompt())
                .Returns((true, fakePassword));

            // 2. Simulamos el diálogo de guardar
            _fileDialogServiceMock
                .Setup(f => f.ShowSaveDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((true, fakeFilePath));

            // Act
            await _viewModel.ExportBackupCommand.ExecuteAsync(null);

            // Assert
            // 3. Verificamos que el servicio de backup fue llamado con los datos correctos
            _backupServiceMock.Verify(
                b => b.ExportBackupAsync(fakeFilePath, fakePassword),
                Times.Once,
                "El servicio de backup no fue llamado con la ruta y contraseña correctas.");
        }

        [TestMethod]
        public async Task ExportBackupAsync_NoDebeLlamarBackupService_SiSeCancelaPassword()
        {
            // Arrange
            // 1. Simulamos la CANCELACIÓN de la contraseña
            _dialogServiceMock
                .Setup(d => d.ShowPasswordPrompt())
                .Returns((false, string.Empty));

            // Act
            await _viewModel.ExportBackupCommand.ExecuteAsync(null);

            // Assert
            // 2. Verificamos que NUNCA se llamó al servicio de backup
            _backupServiceMock.Verify(
                b => b.ExportBackupAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);

            // 3. Verificamos que NUNCA se intentó abrir el diálogo de archivo
            _fileDialogServiceMock.Verify(
                f => f.ShowSaveDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }
    }
}