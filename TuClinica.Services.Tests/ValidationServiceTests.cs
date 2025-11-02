using Microsoft.VisualStudio.TestTools.UnitTesting;
using TuClinica.Services.Implementation; // <-- Para poder usar tu clase
using TuClinica.Core.Interfaces.Services; // <-- Para poder usar la interfaz

namespace TuClinica.Services.Tests
{
    [TestClass] // Le dice a Visual Studio que esto es una clase de pruebas
    public class ValidationServiceTests
    {
        private IValidationService _validationService;

        // Este método se ejecuta ANTES de cada test
        [TestInitialize]
        public void Setup()
        {
            // Creamos una nueva instancia limpia para cada prueba
            _validationService = new ValidationService();
        }

        [TestMethod] // Le dice a VS que esto es una prueba individual
       
        public void IsValidDniNie_DebeDevolverTrue_ParaDniValido()
        {
            // 1. Arrange (Preparar)
            // LÍNEA CORREGIDA:
            string dniValido = "12345678Z";

            // 2. Act (Actuar)
            bool resultado = _validationService.IsValidDniNie(dniValido);

            // 3. Assert (Verificar)
            Assert.IsTrue(resultado, "El DNI válido fue marcado como inválido.");
        }

        [TestMethod]
        public void IsValidDniNie_DebeDevolverTrue_ParaNieValido()
        {
            // 1. Arrange
            string nieValido = "X1234567L"; // (Letra calculada correcta)

            // 2. Act
            bool resultado = _validationService.IsValidDniNie(nieValido);

            // 3. Assert
            Assert.IsTrue(resultado, "El NIE válido fue marcado como inválido.");
        }

        [TestMethod]
        public void IsValidDniNie_DebeDevolverFalse_ParaLetraIncorrecta()
        {
            // 1. Arrange
            string dniLetraIncorrecta = "78782638Z"; // Debería ser 'Y'

            // 2. Act
            bool resultado = _validationService.IsValidDniNie(dniLetraIncorrecta);

            // 3. Assert
            // Verificamos que el resultado es el que esperamos (false)
            Assert.IsFalse(resultado, "Un DNI con letra incorrecta fue marcado como válido.");
        }

        [TestMethod]
        public void IsValidDniNie_DebeDevolverFalse_ParaFormatoIncorrecto()
        {
            // 1. Arrange
            string formatoCorto = "12345";
            string formatoLargo = "123456789Z";
            string formatoLetras = "ABCDEFG";

            // 2. Act
            bool resCorto = _validationService.IsValidDniNie(formatoCorto);
            bool resLargo = _validationService.IsValidDniNie(formatoLargo);
            bool resLetras = _validationService.IsValidDniNie(formatoLetras);

            // 3. Assert
            Assert.IsFalse(resCorto, "Un DNI corto fue marcado como válido.");
            Assert.IsFalse(resLargo, "Un DNI largo fue marcado como válido.");
            Assert.IsFalse(resLetras, "Un DNI de letras fue marcado como válido.");
        }

        [TestMethod]
        public void IsValidDniNie_DebeDevolverFalse_ParaEntradaNulaOVacia()
        {
            // 1. Arrange
            string? dniNulo = null;
            string dniVacio = string.Empty;
            string dniEspacios = "   ";

            // 2. Act
            // El servicio (IsValidDniNie) ya maneja 'null' en la definición del método
            bool resNulo = _validationService.IsValidDniNie(dniNulo);
            bool resVacio = _validationService.IsValidDniNie(dniVacio);
            bool resEspacios = _validationService.IsValidDniNie(dniEspacios);

            // 3. Assert
            Assert.IsFalse(resNulo, "Un DNI nulo fue marcado como válido.");
            Assert.IsFalse(resVacio, "Un DNI vacío fue marcado como válido.");
            Assert.IsFalse(resEspacios, "Un DNI con espacios fue marcado como válido.");
        }
    }
}