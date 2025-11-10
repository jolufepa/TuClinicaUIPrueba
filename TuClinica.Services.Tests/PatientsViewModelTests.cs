using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System; // Añadido para IServiceProvider
using System.Linq.Expressions;
using TuClinica.Core.Interfaces; // Para IRepository
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.UI.ViewModels;

namespace TuClinica.Services.Tests
{
    [TestClass]
    public class PatientsViewModelTests
    {
        // --- Dependencias (Mocks) ---
        private Mock<IPatientRepository> _patientRepoMock;
        private Mock<IValidationService> _validationServiceMock;
        private Mock<IServiceProvider> _serviceProviderMock;
        private PatientFileViewModel _patientFileVM_Instance; // Objeto real, pero con dependencias mockeadas
        private Mock<IActivityLogService> _activityLogServiceMock;
        private Mock<IDialogService> _dialogServiceMock;

        // --- MOCKS NUEVOS (para PatientFileViewModel) ---
        private Mock<IClinicalEntryRepository> _clinicalEntryRepoMock;
        private Mock<IPaymentRepository> _paymentRepoMock;
        private Mock<IRepository<PaymentAllocation>> _allocationRepoMock;
        private Mock<IAuthService> _authServiceMock;
        private Mock<ITreatmentRepository> _treatmentRepoMock;
        private Mock<IFileDialogService> _fileDialogServiceMock;
        private Mock<IPdfService> _pdfServiceMock; // <-- Este Mock se queda, pero no se usa en el constructor

        // --- Objeto a Probar ---
        private PatientsViewModel _viewModel;

        [TestInitialize]
        public void Setup()
        {
            // 1. Inicializamos todos los Mocks
            _patientRepoMock = new Mock<IPatientRepository>();
            _validationServiceMock = new Mock<IValidationService>();
            _serviceProviderMock = new Mock<IServiceProvider>();
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
            // --- FIN MOCKS NUEVOS ---


            // 2. Creamos la instancia de PatientFileViewModel
            // *** CORRECCIÓN CLAVE AQUÍ (CS1729) ***
            // El constructor ahora solo toma 4 argumentos.
            _patientFileVM_Instance = new PatientFileViewModel(
                _authServiceMock.Object,
                _dialogServiceMock.Object,
                _serviceProviderMock.Object,
                _fileDialogServiceMock.Object
            // _pdfServiceMock.Object // <-- ESTE ES EL ARGUMENTO QUE SE ELIMINA
            );
            // *** FIN DE LA CORRECCIÓN ***


            // 3. Creamos el ViewModel pasándole los Mocks
            _viewModel = new PatientsViewModel(
                _patientRepoMock.Object,
                _validationServiceMock.Object,
                _serviceProviderMock.Object,
                _patientFileVM_Instance,
                _activityLogServiceMock.Object,
                _dialogServiceMock.Object
            );
        }

        [TestMethod]
        public void Comandos_SetNewPatientFormCommand_DebeHabilitarFormulario()
        {
            // Arrange
            // Ponemos el ViewModel en un estado "sucio"
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
            // Comprobamos que es una COPIA, no la misma instancia
            Assert.AreNotSame(paciente, _viewModel.PatientFormModel, "El modelo del formulario es la misma instancia que el seleccionado.");
            Assert.AreEqual("Juan", _viewModel.PatientFormModel.Name, "El nombre no se copió al formulario.");
            Assert.AreEqual(123, _viewModel.PatientFormModel.Id, "El Id no se copió al formulario.");
        }

        [TestMethod]
        public async Task SavePatientAsync_NoDebeGuardar_SiDniEsInvalido()
        {
            // Arrange
            // 1. Configuramos el Mock de validación para que devuelva 'false'
            _validationServiceMock
                .Setup(v => v.IsValidDniNie(It.IsAny<string>()))
                .Returns(false);

            // 2. Preparamos un paciente nuevo
            _viewModel.SetNewPatientFormCommand.Execute(null);
            _viewModel.PatientFormModel.DniNie = "DNI_INVALIDO";

            // Act
            await _viewModel.SavePatientCommand.ExecuteAsync(null);

            // Assert
            // 3. Verificamos que el repositorio NUNCA fue llamado para añadir algo
            _patientRepoMock.Verify(
                repo => repo.AddAsync(It.IsAny<Patient>()),
                Times.Never,
                "Se llamó a AddAsync con un DNI inválido.");
        }


        [TestMethod]
        public async Task DeletePatientAsync_DebeHacerSoftDelete_SiTieneHistorial()
        {
            // Arrange
            var pacienteConHistorial = new Patient { Id = 123, Name = "Paciente Antiguo", IsActive = true };
            _viewModel.SelectedPatient = pacienteConHistorial;

            // 1. SIMULAMOS QUE EL USUARIO PRESIONA "SÍ"
            _dialogServiceMock
                .Setup(d => d.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(TuClinica.Core.Interfaces.Services.DialogResult.Yes);

            // 2. Configuramos el Mock para que devuelva que SÍ tiene historial
            _patientRepoMock
                .Setup(repo => repo.HasHistoryAsync(123))
                .ReturnsAsync(true);

            // 3. Necesitamos que GetByIdAsync devuelva al paciente para poder "archivarlo"
            _patientRepoMock
                .Setup(repo => repo.GetByIdAsync(123))
                .ReturnsAsync(pacienteConHistorial);

            // Act
            await _viewModel.DeletePatientAsyncCommand.ExecuteAsync(null);

            // Assert
            // 4. Verificamos que el paciente se marcó como Inactivo
            Assert.IsFalse(pacienteConHistorial.IsActive, "El paciente no se marcó como IsActive = false.");

            // 5. Verificamos que NO se llamó a Remove (Hard-Delete)
            _patientRepoMock.Verify(
                repo => repo.Remove(It.IsAny<Patient>()),
                Times.Never,
                "Se llamó a Remove() en un paciente con historial (debía ser Soft-Delete).");

            // 6. Verificamos que SÍ se guardaron los cambios (para el IsActive = false)
            _patientRepoMock.Verify(
                repo => repo.SaveChangesAsync(),
                Times.Once,
                "No se llamó a SaveChangesAsync() después del Soft-Delete.");
        }
    }
}