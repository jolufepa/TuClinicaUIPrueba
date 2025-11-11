// En: TuClinica.Services.Tests/BudgetsViewModelTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq; // <-- La librería que acabamos de instalar
using System;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.UI.ViewModels;
// --- CAMBIO 1: Añadir using para IServiceScopeFactory ---
using Microsoft.Extensions.DependencyInjection;

namespace TuClinica.Services.Tests
{
    [TestClass]
    public class BudgetsViewModelTests
    {
        // 1. Declaramos variables para nuestros "Mocks" (simulacros)
        private Mock<IPatientRepository> _patientRepoMock;
        private Mock<ITreatmentRepository> _treatmentRepoMock;
        private Mock<IBudgetRepository> _budgetRepoMock;
        private Mock<IPdfService> _pdfServiceMock;

        // --- CAMBIO 2: Renombrar a 'scopeFactoryMock' ---
        private Mock<IServiceScopeFactory> _scopeFactoryMock;

        private Mock<IDialogService> _dialogServiceMock;

        // 2. Declaramos la variable para el ViewModel que vamos a probar
        private BudgetsViewModel _viewModel;

        // 3. Este método [TestInitialize] se ejecuta antes de CADA prueba
        [TestInitialize]
        public void Setup()
        {
            // Creamos instancias "falsas" de todas las dependencias
            _patientRepoMock = new Mock<IPatientRepository>();
            _treatmentRepoMock = new Mock<ITreatmentRepository>();
            _budgetRepoMock = new Mock<IBudgetRepository>();
            _pdfServiceMock = new Mock<IPdfService>();

            // --- CAMBIO 3: Inicializar el mock de la factory ---
            _scopeFactoryMock = new Mock<IServiceScopeFactory>();

            _dialogServiceMock = new Mock<IDialogService>();

            // 4. Creamos el ViewModel, pasándole los objetos "falsos" (.Object)
            // --- CAMBIO 4: Pasar la factory al constructor ---
            _viewModel = new BudgetsViewModel(
                _patientRepoMock.Object,
                _treatmentRepoMock.Object,
                _budgetRepoMock.Object,
                _pdfServiceMock.Object,
                _scopeFactoryMock.Object, // <-- ARGUMENTO MODIFICADO
                _dialogServiceMock.Object
            );
        }

        // --- ¡NUESTROS PRIMEROS TESTS PARA EL VIEWMODEL! ---

        [TestMethod]
        public void Calculos_DebeActualizarSubtotal_AlAnadirItem()
        {
            // Arrange (Preparar)
            // Creamos una línea de presupuesto
            var item = new BudgetLineItem { Quantity = 2, UnitPrice = 100 };

            // Act (Actuar)
            // Añadimos el item a la lista del ViewModel
            _viewModel.BudgetItems.Add(item);

            // Assert (Verificar)
            // Comprobamos que la propiedad 'Subtotal' se ha calculado correctamente
            Assert.AreEqual(200m, _viewModel.Subtotal, "El subtotal no se calculó correctamente.");
        }

        // --- ¡¡TEST MODIFICADO!! ---
        [TestMethod]
        // 1. Cambiamos el nombre del test para reflejar la nueva lógica
        public void Calculos_DebeCalcularTotalCorrectamente_ConDescuentoYVatCERO()
        {
            // Arrange (Preparar)
            // Añadimos un item base de 1000€
            var item = new BudgetLineItem { Quantity = 1, UnitPrice = 1000 };
            _viewModel.BudgetItems.Add(item);

            // Act (Actuar)
            // Aplicamos un 10% de descuento y 0% de IVA
            _viewModel.DiscountPercent = 10;
            _viewModel.VatPercent = 0; // 2. IVA 0%

            // Assert (Verificar)
            // Verificamos todos los cálculos encadenados
            // Subtotal = 1000
            // DiscountAmount = 100 (10% de 1000)
            // Base Imponible = 900 (1000 - 100)
            // VatAmount = 0 (0% de 900) <-- 3. Cálculo corregido
            // TotalAmount = 900 (900 + 0) <-- 4. Cálculo corregido
            Assert.AreEqual(1000m, _viewModel.Subtotal);
            Assert.AreEqual(100m, _viewModel.DiscountAmount);
            Assert.AreEqual(0m, _viewModel.VatAmount); // <-- 5. Assert corregido
            Assert.AreEqual(900m, _viewModel.TotalAmount, "El cálculo del total con IVA 0% y descuento es incorrecto."); // <-- 6. Assert corregido
        }

        [TestMethod]
        public void Calculos_DebeRecalcularSubtotal_AlModificarItem()
        {
            // Arrange (Preparar)
            var item = new BudgetLineItem { Quantity = 1, UnitPrice = 100 };
            _viewModel.BudgetItems.Add(item);

            // Verificamos el estado inicial
            Assert.AreEqual(100m, _viewModel.Subtotal);

            // Act (Actuar)
            // Modificamos la cantidad del item (simulando lo que hace el DataGrid)
            item.Quantity = 3;

            // Assert (Verificar)
            // El ViewModel debería detectar este cambio y recalcular
            Assert.AreEqual(300m, _viewModel.Subtotal, "El Subtotal no se recalculó al cambiar la cantidad del item.");
        }
    }
}