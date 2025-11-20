using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Linq.Expressions;
using TuClinica.Core.Interfaces;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using TuClinica.Core.Enums;
using System.Threading;
using System.Collections.Generic; // Necesario para List<>

namespace TuClinica.Services.Tests
{
    [TestClass]
    public class PatientsViewModelTests
    {
        // --- Dependencias (Mocks) ---
        private Mock<IPatientRepository> _patientRepoMock;
        private Mock<IValidationService> _validationServiceMock;
        private Mock<IServiceScopeFactory> _scopeFactoryMock;
        private PatientFileViewModel _patientFileVM_Instance;
        private Mock<IActivityLogService> _activityLogServiceMock;
        private Mock<IDialogService> _dialogServiceMock;

        // --- MOCKS NUEVOS (para PatientFileViewModel) ---
        private Mock<IClinicalEntryRepository> _clinicalEntryRepoMock;
        private Mock<IPaymentRepository> _paymentRepoMock;
        private Mock<IRepository<PaymentAllocation>> _allocationRepoMock;
        private Mock<IAuthService> _authServiceMock;
        private Mock<ITreatmentRepository> _treatmentRepoMock;
        private Mock<IFileDialogService> _fileDialogServiceMock;
        private Mock<IPdfService> _pdfServiceMock;
        private Mock<IPatientAlertRepository> _alertRepoMock;


        // --- Objeto a Probar ---
        private PatientsViewModel _viewModel;

        [TestInitialize]
        public void Setup()
        {
            // 1. Inicializamos todos los Mocks
            _patientRepoMock = new Mock<IPatientRepository>();
            _validationServiceMock = new Mock<IValidationService>();
            _scopeFactoryMock = new Mock<IServiceScopeFactory>();
            _activityLogServiceMock = new Mock<IActivityLogService>();
            _dialogServiceMock = new Mock<IDialogService>();

            // --- MOCKS NUEVOS ---
            _clinicalEntryRepoMock = new Mock<IClinicalEntryRepository>();
            _paymentRepoMock = new Mock<IPaymentRepository>();
            _allocationRepoMock = new Mock<IRepository<PaymentAllocation>>();
            _authServiceMock = new Mock<IAuthService>();
            _treatmentRepoMock = new Mock<ITreatmentRepository>();
            _fileDialogServiceMock = new Mock<IFileDialogService>();
            _pdfServiceMock = new Mock<IPdfService>();
            _alertRepoMock = new Mock<IPatientAlertRepository>();

            // 2. Creamos la instancia de PatientFileViewModel
            // *** CORRECCIÓN: AÑADIDO EL ÚLTIMO ARGUMENTO _pdfServiceMock.Object ***
            _patientFileVM_Instance = new PatientFileViewModel(
                _authServiceMock.Object,            // 1. IAuthService
                _dialogServiceMock.Object,          // 2. IDialogService
                _scopeFactoryMock.Object,           // 3. IServiceScopeFactory
                _fileDialogServiceMock.Object,      // 4. IFileDialogService
                _validationServiceMock.Object,      // 5. IValidationService
                _alertRepoMock.Object,              // 6. IPatientAlertRepository
                _pdfServiceMock.Object              // 7. IPdfService (¡ESTE FALTABA!)
            );


            // 3. Creamos el ViewModel pasándole los Mocks
            _viewModel = new PatientsViewModel(
                _patientRepoMock.Object,
                _validationServiceMock.Object,
                _scopeFactoryMock.Object,
                _patientFileVM_Instance,
                _activityLogServiceMock.Object,
                _dialogServiceMock.Object
            );

            // Configurar el mock de alertas para que devuelva una lista vacía y no falle en la carga inicial
            _alertRepoMock.Setup(r => r.GetActiveAlertsForPatientAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(new List<PatientAlert>());
        }

        [TestMethod]
        public void Comandos_SetNewPatientFormCommand_DebeHabilitarFormulario()
        {
            // Arrange
            _viewModel.IsFormEnabled = false;
            _viewModel.SelectedPatient = new Patient { Id = 1, Name = "Test" };

            // Act
            _viewModel.SetNewPatientFormCommand.Execute(null);

            // Assert
            Assert.IsTrue(_viewModel.IsFormEnabled, "El formulario no se habilitó.");
            Assert.IsNull(_viewModel.SelectedPatient, "El paciente seleccionado no se limpió.");
            Assert.AreEqual(0, _viewModel.PatientFormModel.Id, "El PatientFormModel no es un paciente nuevo.");
        }

        [TestMethod]
        public void Comandos_EditPatientCommand_DebeCopiarDatosAlFormulario()
        {
            // Arrange
            var paciente = new Patient { Id = 123, Name = "Juan", Surname = "Perez" };
            _viewModel.SelectedPatient = paciente;

            // Act
            _viewModel.EditPatientCommand.Execute(null);

            // Assert
            Assert.IsTrue(_viewModel.IsFormEnabled, "El formulario no se habilitó.");
            Assert.AreNotSame(paciente, _viewModel.PatientFormModel, "El modelo del formulario es la misma instancia que el seleccionado.");
            Assert.AreEqual("Juan", _viewModel.PatientFormModel.Name, "El nombre no se copió al formulario.");
            Assert.AreEqual(123, _viewModel.PatientFormModel.Id, "El Id no se copió al formulario.");
        }

        [TestMethod]
        public async Task SavePatientAsync_NoDebeGuardar_SiDocumentoEsInvalido()
        {
            // Arrange
            _validationServiceMock
                .Setup(v => v.IsValidDocument(It.IsAny<string>(), It.IsAny<PatientDocumentType>()))
                .Returns(false);

            _viewModel.SetNewPatientFormCommand.Execute(null);
            _viewModel.PatientFormModel.DocumentNumber = "DNI_INVALIDO";
            _viewModel.PatientFormModel.DocumentType = PatientDocumentType.DNI;

            // Act
            await _viewModel.SavePatientCommand.ExecuteAsync(null);

            // Assert
            _patientRepoMock.Verify(
                repo => repo.AddAsync(It.IsAny<Patient>()),
                Times.Never,
                "Se llamó a AddAsync con un Documento inválido.");
        }


        [TestMethod]
        public async Task DeletePatientAsync_DebeHacerSoftDelete_SiTieneHistorial()
        {
            // Arrange
            var pacienteConHistorial = new Patient { Id = 123, Name = "Paciente Antiguo", IsActive = true };
            _viewModel.SelectedPatient = pacienteConHistorial;

            _dialogServiceMock
                .Setup(d => d.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(TuClinica.Core.Interfaces.Services.DialogResult.Yes);

            _patientRepoMock
                .Setup(repo => repo.HasHistoryAsync(123))
                .ReturnsAsync(true);

            _patientRepoMock
                .Setup(repo => repo.GetByIdAsync(123))
                .ReturnsAsync(pacienteConHistorial);

            _clinicalEntryRepoMock.Setup(r => r.GetTotalChargedForPatientAsync(123)).ReturnsAsync(0);
            _paymentRepoMock.Setup(r => r.GetTotalPaidForPatientAsync(123)).ReturnsAsync(0);

            var scopeMock = new Mock<IServiceScope>();
            var serviceProviderMock = new Mock<IServiceProvider>();
            scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
            _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IClinicalEntryRepository))).Returns(_clinicalEntryRepoMock.Object);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IPaymentRepository))).Returns(_paymentRepoMock.Object);


            // Act
            await _viewModel.DeletePatientAsyncCommand.ExecuteAsync(null);

            // Assert
            Assert.IsFalse(pacienteConHistorial.IsActive, "El paciente no se marcó como IsActive = false.");

            _patientRepoMock.Verify(
                repo => repo.Remove(It.IsAny<Patient>()),
                Times.Never,
                "Se llamó a Remove() en un paciente con historial (debía ser Soft-Delete).");

            _patientRepoMock.Verify(
                repo => repo.SaveChangesAsync(),
                Times.Once,
                "No se llamó a SaveChangesAsync() después del Soft-Delete.");
        }
    }
}