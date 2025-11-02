using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Models;
using TuClinica.Services.Implementation;
using Microsoft.Extensions.DependencyInjection; // Para IServiceProvider
using System;

namespace TuClinica.Services.Tests
{
    [TestClass]
    public class AuthServiceTests
    {
        // --- Dependencias (Mocks) ---
        private Mock<IServiceProvider> _serviceProviderMock;
        private Mock<IServiceScopeFactory> _scopeFactoryMock;
        private Mock<IServiceScope> _scopeMock;
        private Mock<IServiceProvider> _scopedServiceProviderMock;
        private Mock<IInactivityService> _inactivityServiceMock;
        private Mock<IUserRepository> _userRepoMock;

        // --- Servicio a Probar ---
        private AuthService _authService;

        // --- Datos de Prueba ---
        private User _dummyUser;
        private readonly string _correctPassword = "admin123";
        private string _correctHash;

        [TestInitialize]
        public void Setup()
        {
            // 1. Pre-calculamos un hash real para nuestras pruebas
            _correctHash = BCrypt.Net.BCrypt.HashPassword(_correctPassword);
            _dummyUser = new User
            {
                Id = 1,
                Username = "admin",
                HashedPassword = _correctHash,
                IsActive = true
            };

            // 2. Inicializamos todos los Mocks
            _serviceProviderMock = new Mock<IServiceProvider>();
            _scopeFactoryMock = new Mock<IServiceScopeFactory>();
            _scopeMock = new Mock<IServiceScope>();
            _scopedServiceProviderMock = new Mock<IServiceProvider>();
            _inactivityServiceMock = new Mock<IInactivityService>();
            _userRepoMock = new Mock<IUserRepository>();

            // 3. Configuramos la cadena de Mocks para simular _serviceProvider.CreateScope()
            //    que es lo que usa tu AuthService

            // 3.1. El IServiceProvider principal debe devolver un IServiceScopeFactory
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(IServiceScopeFactory)))
                                .Returns(_scopeFactoryMock.Object);

            // 3.2. El IServiceScopeFactory debe crear un IServiceScope
            _scopeFactoryMock.Setup(sf => sf.CreateScope()).Returns(_scopeMock.Object);

            // 3.3. El IServiceScope debe devolver un IServiceProvider "con ámbito"
            _scopeMock.Setup(s => s.ServiceProvider).Returns(_scopedServiceProviderMock.Object);

            // 3.4. El IServiceProvider "con ámbito" debe devolver nuestro IUserRepository falso
            _scopedServiceProviderMock.Setup(sp => sp.GetService(typeof(IUserRepository)))
                                      .Returns(_userRepoMock.Object);

            // 4. Finalmente, creamos la instancia del AuthService
            _authService = new AuthService(
                _serviceProviderMock.Object,
                _inactivityServiceMock.Object
            );
        }

        [TestMethod]
        public async Task LoginAsync_DebeDevolverFalse_SiUsuarioNoExiste()
        {
            // Arrange
            // Configuramos el Mock del repositorio para que devuelva 'null'
            _userRepoMock.Setup(repo => repo.GetByUsernameAsync("usuario_falso"))
                         .ReturnsAsync((User)null);

            // Act
            bool resultado = await _authService.LoginAsync("usuario_falso", "password");

            // Assert
            Assert.IsFalse(resultado, "El login debió fallar para un usuario inexistente.");
            Assert.IsNull(_authService.CurrentUser, "CurrentUser no debió establecerse.");
        }

        [TestMethod]
        public async Task LoginAsync_DebeDevolverFalse_SiPasswordEsIncorrecta()
        {
            // Arrange
            // Configuramos el Mock para que devuelva nuestro usuario de prueba
            _userRepoMock.Setup(repo => repo.GetByUsernameAsync("admin"))
                         .ReturnsAsync(_dummyUser);

            // Act
            // Pasamos una contraseña incorrecta
            bool resultado = await _authService.LoginAsync("admin", "password_incorrecta");

            // Assert
            Assert.IsFalse(resultado, "El login debió fallar para una contraseña incorrecta.");
            Assert.IsNull(_authService.CurrentUser, "CurrentUser no debió establecerse.");
        }

        [TestMethod]
        public async Task LoginAsync_DebeDevolverTrue_SiCredencialesSonCorrectas()
        {
            // Arrange
            // Configuramos el Mock para que devuelva nuestro usuario de prueba
            _userRepoMock.Setup(repo => repo.GetByUsernameAsync("admin"))
                         .ReturnsAsync(_dummyUser);

            // Act
            // Pasamos la contraseña correcta
            bool resultado = await _authService.LoginAsync("admin", _correctPassword);

            // Assert
            Assert.IsTrue(resultado, "El login debió ser exitoso.");
            Assert.IsNotNull(_authService.CurrentUser, "CurrentUser debió establecerse.");
            Assert.AreEqual("admin", _authService.CurrentUser.Username, "El usuario incorrecto fue establecido.");
        }

        [TestMethod]
        public async Task Logout_DebeLimpiarCurrentUser_YPararTimer()
        {
            // Arrange
            // Forzamos un login primero para establecer el CurrentUser
            _userRepoMock.Setup(repo => repo.GetByUsernameAsync("admin")).ReturnsAsync(_dummyUser);
            await _authService.LoginAsync("admin", _correctPassword);
            Assert.IsNotNull(_authService.CurrentUser, "Setup fallido: El login no estableció CurrentUser.");

            // Act
            _authService.Logout();

            // Assert
            Assert.IsNull(_authService.CurrentUser, "CurrentUser no se limpió después de Logout.");

            // Verificamos que el servicio de inactividad fue llamado para detenerse
            _inactivityServiceMock.Verify(
                service => service.Stop(),
                Times.Once,
                "No se llamó a Stop() en IInactivityService durante el Logout.");
        }
    }
}
