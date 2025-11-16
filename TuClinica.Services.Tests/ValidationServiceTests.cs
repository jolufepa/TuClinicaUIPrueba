using Microsoft.VisualStudio.TestTools.UnitTesting;
using TuClinica.Services.Implementation; // <-- Para poder usar tu clase
using TuClinica.Core.Interfaces.Services; // <-- Para poder usar la interfaz
// --- INICIO DE LA MODIFICACIÓN ---
using TuClinica.Core.Enums; // <-- AÑADIDO para poder usar PatientDocumentType
// --- FIN DE LA MODIFICACIÓN ---

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

        // --- Pruebas antiguas para IsValidDniNie (siguen siendo útiles) ---

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

        // --- INICIO DE LA MODIFICACIÓN (Nuevas Pruebas) ---

        [TestMethod]
        public void IsValidDocument_DebeValidarDNI_Correctamente()
        {
            // Arrange
            string dniValido = "12345678Z";
            string dniInvalido = "12345678A";

            // Act
            bool resValido = _validationService.IsValidDocument(dniValido, PatientDocumentType.DNI);
            bool resInvalido = _validationService.IsValidDocument(dniInvalido, PatientDocumentType.DNI);

            // Assert
            Assert.IsTrue(resValido, "El DNI válido fue marcado como inválido por IsValidDocument.");
            Assert.IsFalse(resInvalido, "El DNI inválido fue marcado como válido por IsValidDocument.");
        }

        [TestMethod]
        public void IsValidDocument_DebeValidarNIE_Correctamente()
        {
            // Arrange
            string nieValido = "X1234567L";
            string nieInvalido = "X1234567A";

            // Act
            bool resValido = _validationService.IsValidDocument(nieValido, PatientDocumentType.NIE);
            bool resInvalido = _validationService.IsValidDocument(nieInvalido, PatientDocumentType.NIE);

            // Assert
            Assert.IsTrue(resValido, "El NIE válido fue marcado como inválido por IsValidDocument.");
            Assert.IsFalse(resInvalido, "El NIE inválido fue marcado como válido por IsValidDocument.");
        }

        [TestMethod]
        public void IsValidDocument_DebeValidarPasaporte_Correctamente()
        {
            // Arrange
            string pasaporteValido = "PASS12345";
            string pasaporteInvalido = "ABC"; // Menos de 4 caracteres

            // Act
            bool resValido = _validationService.IsValidDocument(pasaporteValido, PatientDocumentType.Pasaporte);
            bool resInvalido = _validationService.IsValidDocument(pasaporteInvalido, PatientDocumentType.Pasaporte);
            bool resNulo = _validationService.IsValidDocument(null, PatientDocumentType.Pasaporte);

            // Assert
            Assert.IsTrue(resValido, "El Pasaporte válido fue marcado como inválido.");
            Assert.IsFalse(resInvalido, "Un Pasaporte corto fue marcado como válido.");
            Assert.IsFalse(resNulo, "Un Pasaporte nulo fue marcado como válido.");
        }

        [TestMethod]
        public void IsValidDocument_DebeValidarOtro_Correctamente()
        {
            // Arrange
            string otroValido = "ID-Extranjero-123";
            string otroInvalido = "123"; // Menos de 4 caracteres

            // Act
            bool resValido = _validationService.IsValidDocument(otroValido, PatientDocumentType.Otro);
            bool resInvalido = _validationService.IsValidDocument(otroInvalido, PatientDocumentType.Otro);

            // Assert
            Assert.IsTrue(resValido, "El 'Otro' documento válido fue marcado como inválido.");
            Assert.IsFalse(resInvalido, "Un 'Otro' documento corto fue marcado como válido.");
        }
        // --- FIN DE LA MODIFICACIÓN ---
    }
}