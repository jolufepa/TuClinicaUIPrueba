using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
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
    public class TreatmentsViewModelTests
    {
        private Mock<ITreatmentRepository> _treatmentRepoMock;
        private Mock<IDialogService> _dialogServiceMock;
        private Mock<IRepository<TreatmentPackItem>> _packItemRepoMock;
        private TreatmentsViewModel _viewModel;

        [TestInitialize]
        public void Setup()
        {
            _treatmentRepoMock = new Mock<ITreatmentRepository>();
            _dialogServiceMock = new Mock<IDialogService>();
            _packItemRepoMock = new Mock<IRepository<TreatmentPackItem>>();

            // Configuramos el repositorio para que devuelva una lista vacía por defecto al cargar
            _treatmentRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Treatment>());

            _viewModel = new TreatmentsViewModel(
                _treatmentRepoMock.Object,
                _dialogServiceMock.Object,
                _packItemRepoMock.Object);
        }

        [TestMethod]
        public void AddPackItem_NoDebePermitir_AgregarTratamientoASiMismo()
        {
            // Arrange
            var tratamientoPrincipal = new Treatment { Id = 10, Name = "Limpieza Pack" };
            var tratamientoHijo = new Treatment { Id = 10, Name = "Limpieza Pack" }; // Mismo ID

            // Simulamos que estamos editando el tratamiento 10
            _viewModel.SelectedTreatment = tratamientoPrincipal;

            // Seleccionamos el mismo tratamiento en el combo para añadirlo como hijo
            _viewModel.SelectedComponentToAdd = tratamientoHijo;
            _viewModel.ComponentQuantity = 1;

            // Act
            _viewModel.AddPackItemCommand.Execute(null);

            // Assert
            // 1. La lista de items del pack debe seguir vacía
            Assert.AreEqual(0, _viewModel.CurrentPackItems.Count, "No se debió añadir el item.");

            // 2. Se debe haber mostrado un mensaje de error
            _dialogServiceMock.Verify(d => d.ShowMessage(
                It.Is<string>(s => s.Contains("mismo") || s.Contains("sí mismo")),
                It.IsAny<string>(),
                It.IsAny<DialogResult>()), Times.Once);
        }

        [TestMethod]
        public void AddPackItem_NoDebePermitir_CantidadCeroONegativa()
        {
            // Arrange
            var tratamientoHijo = new Treatment { Id = 5, Name = "Hijo" };
            _viewModel.SelectedComponentToAdd = tratamientoHijo;
            _viewModel.ComponentQuantity = 0; // Cantidad inválida

            // Act
            _viewModel.AddPackItemCommand.Execute(null);

            // Assert
            Assert.AreEqual(0, _viewModel.CurrentPackItems.Count);
            _dialogServiceMock.Verify(d => d.ShowMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DialogResult>()), Times.Once);
        }

        [TestMethod]
        public void AddPackItem_DebeAgregar_SiDatosSonValidos()
        {
            // Arrange
            var tratamientoPrincipal = new Treatment { Id = 1, Name = "Pack" };
            var tratamientoHijo = new Treatment { Id = 2, Name = "Hijo" };

            _viewModel.SelectedTreatment = tratamientoPrincipal;
            _viewModel.SelectedComponentToAdd = tratamientoHijo;
            _viewModel.ComponentQuantity = 5;

            // Act
            _viewModel.AddPackItemCommand.Execute(null);

            // Assert
            Assert.AreEqual(1, _viewModel.CurrentPackItems.Count);
            Assert.AreEqual(5, _viewModel.CurrentPackItems[0].Quantity);
            Assert.AreEqual(2, _viewModel.CurrentPackItems[0].ChildTreatmentId);
        }
    }
}