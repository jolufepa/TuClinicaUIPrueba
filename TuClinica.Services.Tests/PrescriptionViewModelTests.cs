using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.UI.ViewModels;

namespace TuClinica.Services.Tests
{
    [TestClass]
    public class PrescriptionViewModelTests
    {
        // Mocks de infraestructura
        private Mock<IServiceScopeFactory> _scopeFactoryMock;
        private Mock<IServiceScope> _scopeMock;
        private Mock<IServiceProvider> _serviceProviderMock;

        // Mocks de servicios
        private Mock<IPdfService> _pdfServiceMock;
        private Mock<IMedicationRepository> _medicationRepoMock;
        private Mock<IDosageRepository> _dosageRepoMock;
        private Mock<IRepository<Prescription>> _prescriptionRepoMock;
        private Mock<IDialogService> _dialogServiceMock;
        private Mock<ISettingsService> _settingsServiceMock;
        private Mock<IAuthService> _authServiceMock; // Necesario dentro del scope

        private PrescriptionViewModel _viewModel;

        [TestInitialize]
        public void Setup()
        {
            // 1. Inicializar Mocks
            _scopeFactoryMock = new Mock<IServiceScopeFactory>();
            _scopeMock = new Mock<IServiceScope>();
            _serviceProviderMock = new Mock<IServiceProvider>();

            _pdfServiceMock = new Mock<IPdfService>();
            _medicationRepoMock = new Mock<IMedicationRepository>();
            _dosageRepoMock = new Mock<IDosageRepository>();
            _prescriptionRepoMock = new Mock<IRepository<Prescription>>();
            _dialogServiceMock = new Mock<IDialogService>();
            _settingsServiceMock = new Mock<ISettingsService>();
            _authServiceMock = new Mock<IAuthService>();

            // 2. Configurar ScopeFactory para devolver nuestro ServiceProvider simulado
            _scopeFactoryMock.Setup(s => s.CreateScope()).Returns(_scopeMock.Object);
            _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);

            // 3. Configurar qué servicios devuelve el ServiceProvider cuando se le piden
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(IAuthService))).Returns(_authServiceMock.Object);

            // Configuración por defecto
            _settingsServiceMock.Setup(s => s.GetSettings()).Returns(new AppSettings { ClinicName = "Test Clinic" });
            _medicationRepoMock.Setup(r => r.GetAllActiveAsync()).ReturnsAsync(new List<Medication>());
            _dosageRepoMock.Setup(r => r.GetAllActiveAsync()).ReturnsAsync(new List<Dosage>());

            // 4. Instanciar ViewModel
            _viewModel = new PrescriptionViewModel(
                _scopeFactoryMock.Object,
                _pdfServiceMock.Object,
                _medicationRepoMock.Object,
                _dosageRepoMock.Object,
                _prescriptionRepoMock.Object,
                _dialogServiceMock.Object,
                _settingsServiceMock.Object
            );
        }

        [TestMethod]
        public void CanGeneratePrescription_DebeSerFalso_Inicialmente()
        {
            // El comando AsyncRelayCommand evalúa CanExecute
            Assert.IsFalse(_viewModel.GeneratePrescriptionPdfCommand.CanExecute(null));
        }

        [TestMethod]
        public void CanGeneratePrescription_DebeSerVerdadero_CuandoHayDatosCompletos()
        {
            // Arrange
            _viewModel.SelectedPatient = new Patient { Id = 1, Name = "Pepe" };
            _viewModel.MedicationSearchText = "Ibuprofeno";
            _viewModel.DosageSearchText = "1 cada 8h";

            // Act & Assert
            Assert.IsTrue(_viewModel.GeneratePrescriptionPdfCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task GeneratePrescriptionPdf_DebeGuardarEnBD_Y_GenerarPDF()
        {
            // Arrange
            _viewModel.SelectedPatient = new Patient { Id = 1, Name = "Pepe", Surname = "Loco" };
            _viewModel.MedicationSearchText = "Paracetamol";
            _viewModel.DosageSearchText = "1 diaria";
            _viewModel.DurationInDays = 5;
            _viewModel.MedicationQuantity = "1 caja";

            // Simulamos que PDF Service devuelve una ruta
            _pdfServiceMock.Setup(p => p.GeneratePrescriptionPdfAsync(It.IsAny<Prescription>()))
                .ReturnsAsync("C:\\temp\\receta.pdf");

            // Act
            await _viewModel.GeneratePrescriptionPdfCommand.ExecuteAsync(null);

            // Assert
            // 1. Verificar que se guardó en el repositorio de recetas
            _prescriptionRepoMock.Verify(r => r.AddAsync(It.Is<Prescription>(p =>
                p.PatientId == 1 &&
                p.Items.Count == 1 &&
                p.Items.First().MedicationName == "Paracetamol"
            )), Times.Once);

            _prescriptionRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);

            // 2. Verificar que se llamó al servicio de PDF
            _pdfServiceMock.Verify(p => p.GeneratePrescriptionPdfAsync(It.IsAny<Prescription>()), Times.Once);

            // 3. Verificar mensaje de éxito
            _dialogServiceMock.Verify(d => d.ShowMessage(It.IsAny<string>(), "Receta Generada", DialogResult.OK), Times.Once);
        }
    }
}