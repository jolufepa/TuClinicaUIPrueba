using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
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
        private Mock<IServiceScopeFactory> _scopeFactoryMock;
        private Mock<IBackupService> _backupServiceMock;
        private Mock<IRepository<ActivityLog>> _logRepoMock;
        private Mock<IActivityLogService> _activityLogServiceMock;
        private Mock<IDialogService> _dialogServiceMock;
        private Mock<IFileDialogService> _fileDialogServiceMock;
        private Mock<ISettingsService> _settingsServiceMock;

        // --- SUT ---
        private AdminViewModel _viewModel;

        [TestInitialize]
        public void Setup()
        {
            _userRepoMock = new Mock<IUserRepository>();
            _scopeFactoryMock = new Mock<IServiceScopeFactory>();
            _backupServiceMock = new Mock<IBackupService>();
            _logRepoMock = new Mock<IRepository<ActivityLog>>();
            _activityLogServiceMock = new Mock<IActivityLogService>();
            _dialogServiceMock = new Mock<IDialogService>();
            _fileDialogServiceMock = new Mock<IFileDialogService>();
            _settingsServiceMock = new Mock<ISettingsService>();

            // Configuración básica para evitar nulls
            _logRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ActivityLog>());
            _settingsServiceMock.Setup(s => s.GetSettings()).Returns(new AppSettings());

            _viewModel = new AdminViewModel(
                _userRepoMock.Object,
                _scopeFactoryMock.Object,
                _backupServiceMock.Object,
                _logRepoMock.Object,
                _activityLogServiceMock.Object,
                _dialogServiceMock.Object,
                _fileDialogServiceMock.Object,
                _settingsServiceMock.Object
            );
        }

        [TestMethod]
        public async Task PurgeOldLogsAsync_DebeLlamarAlServicio_SiUsuarioConfirma()
        {
            // Arrange
            _dialogServiceMock
                .Setup(d => d.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(CoreDialogResult.Yes);

            var expectedDate = DateTime.UtcNow.AddYears(-2);

            // Act
            await _viewModel.PurgeOldLogsCommand.ExecuteAsync(null);

            // Assert
            _activityLogServiceMock.Verify(
                s => s.PurgeOldLogsAsync(It.Is<DateTime>(d => (d - expectedDate).TotalSeconds < 1)),
                Times.Once);
        }

        // --- TESTS ACTUALIZADOS PARA EL NUEVO SISTEMA DE BACKUP (ZIP) ---

       

        [TestMethod]
        public async Task CreateBackupAsync_DebeLlamarServicio_SiDialogoExitoso()
        {
            // Arrange
            string fakeFilePath = "C:\\backup.zip";
            string fakePass = "secreto"; // Contraseña simulada

            // Simulamos que el usuario introduce contraseña
            _dialogServiceMock.Setup(d => d.ShowPasswordPrompt()).Returns((true, fakePass));

            // Simulamos que el usuario elige un archivo
            _fileDialogServiceMock
                .Setup(f => f.ShowSaveDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((true, fakeFilePath));

            // Act
            await _viewModel.CreateBackupCommand.ExecuteAsync(null);

            // Assert
            _backupServiceMock.Verify(
                b => b.CreateBackupAsync(fakeFilePath, fakePass), // Verificamos que pasa la contraseña
                Times.Once);
        }

        [TestMethod]
        public async Task RestoreBackupAsync_DebeLlamarServicio_SiTodoCorrecto()
        {
            // Arrange
            string fakeFilePath = "C:\\backup.zip";
            string fakePass = "secreto";

            _dialogServiceMock.Setup(d => d.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>())).Returns(CoreDialogResult.Yes);
            _fileDialogServiceMock.Setup(f => f.ShowOpenDialog(It.IsAny<string>(), It.IsAny<string>())).Returns((true, fakeFilePath));
            _dialogServiceMock.Setup(d => d.ShowPasswordPrompt()).Returns((true, fakePass));

            // Act
            await _viewModel.RestoreBackupCommand.ExecuteAsync(null);

            // Assert
            _backupServiceMock.Verify(
                b => b.RestoreBackupAsync(fakeFilePath, fakePass),
                Times.Once);
        }
    }
}