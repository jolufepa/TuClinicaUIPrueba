using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces; // Para IRepository genérico
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace TuClinica.Services.Tests
{
    [TestClass]
    public class PatientFinancialViewModelTests
    {
        // Mocks de infraestructura
        private Mock<IServiceScopeFactory> _scopeFactoryMock;
        private Mock<IServiceScope> _scopeMock;
        private Mock<IServiceProvider> _serviceProviderMock;
        private Mock<IDialogService> _dialogServiceMock;
        private Mock<IAuthService> _authServiceMock;
        private Mock<IPdfService> _pdfServiceMock;

        // Mocks de Repositorios necesarios para Finanzas
        private Mock<IClinicalEntryRepository> _entryRepoMock;
        private Mock<IPaymentRepository> _paymentRepoMock;
        private Mock<ITreatmentRepository> _treatmentRepoMock;
        private Mock<IRepository<PaymentAllocation>> _allocationRepoMock;

        // El ViewModel a probar
        private PatientFinancialViewModel _viewModel;
        private Patient _testPatient;

        [TestInitialize]
        public void Setup()
        {
            // 1. Inicializar Mocks
            _scopeFactoryMock = new Mock<IServiceScopeFactory>();
            _scopeMock = new Mock<IServiceScope>();
            _serviceProviderMock = new Mock<IServiceProvider>();
            _dialogServiceMock = new Mock<IDialogService>();
            _authServiceMock = new Mock<IAuthService>();
            _pdfServiceMock = new Mock<IPdfService>();

            _entryRepoMock = new Mock<IClinicalEntryRepository>();
            _paymentRepoMock = new Mock<IPaymentRepository>();
            _treatmentRepoMock = new Mock<ITreatmentRepository>();
            _allocationRepoMock = new Mock<IRepository<PaymentAllocation>>();

            // 2. Configurar el Scope Factory para devolver nuestros repositorios mockeados
            _scopeFactoryMock.Setup(s => s.CreateScope()).Returns(_scopeMock.Object);
            _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);

            // Cuando el VM pida un repositorio al ServiceProvider, le damos el Mock
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(IClinicalEntryRepository))).Returns(_entryRepoMock.Object);
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(IPaymentRepository))).Returns(_paymentRepoMock.Object);
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ITreatmentRepository))).Returns(_treatmentRepoMock.Object);
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(IRepository<PaymentAllocation>))).Returns(_allocationRepoMock.Object);

            // 3. Instanciar el ViewModel
            _viewModel = new PatientFinancialViewModel(
                _scopeFactoryMock.Object,
                _dialogServiceMock.Object,
                _authServiceMock.Object,
                _pdfServiceMock.Object
            );

            // Datos de prueba
            _testPatient = new Patient { Id = 1, Name = "Test", Surname = "Patient" };
        }

        [TestMethod]
        public async Task LoadAsync_DebeCalcularSaldosCorrectamente()
        {
            // Arrange
            decimal totalCargado = 1000m;
            decimal totalPagado = 400m;
            decimal saldoEsperado = 600m; // 1000 - 400

            // Simulamos que el repositorio devuelve estos totales
            _entryRepoMock.Setup(r => r.GetTotalChargedForPatientAsync(_testPatient.Id))
                .ReturnsAsync(totalCargado);
            _paymentRepoMock.Setup(r => r.GetTotalPaidForPatientAsync(_testPatient.Id))
                .ReturnsAsync(totalPagado);

            // Simulamos listas vacías para el historial (no afectan al total en este test unitario específico, 
            // ya que el VM usa el método GetTotal... para las propiedades de cabecera)
            _entryRepoMock.Setup(r => r.GetHistoryForPatientAsync(_testPatient.Id))
                .ReturnsAsync(new List<ClinicalEntry>());
            _paymentRepoMock.Setup(r => r.GetPaymentsForPatientAsync(_testPatient.Id))
                .ReturnsAsync(new List<Payment>());

            _treatmentRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Treatment>());

            // Act
            await _viewModel.LoadAsync(_testPatient, CancellationToken.None);

            // Assert
            Assert.AreEqual(totalCargado, _viewModel.TotalCharged, "TotalCharged incorrecto");
            Assert.AreEqual(totalPagado, _viewModel.TotalPaid, "TotalPaid incorrecto");
            Assert.AreEqual(saldoEsperado, _viewModel.CurrentBalance, "CurrentBalance incorrecto");
        }

        [TestMethod]
        public async Task AllocatePayment_NoDebeGuardar_SiMontoEsCero()
        {
            // Arrange
            // Configuramos una selección válida pero con monto 0
            _viewModel.SelectedCharge = new ClinicalEntry { Id = 1, TotalCost = 100 };
            _viewModel.SelectedPayment = new Payment { Id = 1, Amount = 100 };
            _viewModel.AmountToAllocate = 0;

            // Act
            // Ejecutamos el comando (que internamente llama a AllocatePayment)
            await _viewModel.AllocatePaymentCommand.ExecuteAsync(null);

            // Assert
            // Verificamos que NUNCA se llamó a AddAsync en el repositorio de asignaciones
            _allocationRepoMock.Verify(r => r.AddAsync(It.IsAny<PaymentAllocation>()), Times.Never);

            // Verificamos que no se guardaron cambios
            _allocationRepoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        [TestMethod]
        public async Task AllocatePayment_DebeGuardar_SiDatosSonValidos()
        {
            // Arrange
            _viewModel.SelectedCharge = new ClinicalEntry { Id = 10, TotalCost = 500 }; // Debe 500
            _viewModel.SelectedPayment = new Payment { Id = 20, Amount = 200 };         // Paga 200
            _viewModel.AmountToAllocate = 50; // Asigna 50

            // Act
            await _viewModel.AllocatePaymentCommand.ExecuteAsync(null);

            // Assert
            // Verificamos que se llamó a AddAsync con los datos correctos
            _allocationRepoMock.Verify(r => r.AddAsync(It.Is<PaymentAllocation>(a =>
                a.ClinicalEntryId == 10 &&
                a.PaymentId == 20 &&
                a.AmountAllocated == 50
            )), Times.Once);

            // Verificamos que se guardó en BD
            _allocationRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }
    }
}