using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.DataAccess;
using TuClinica.DataAccess.Repositories;
using TuClinica.Services.Implementation;

namespace TuClinica.Services.Tests
{
    [TestClass]
    public class BackupServiceTests
    {
        private AppDbContext _context;
        private Mock<ICryptoService> _cryptoServiceMock;

        // Repositorios reales (usando el contexto en memoria) para pasar al constructor
        private PatientRepository _patientRepo;
        private TreatmentRepository _treatmentRepo;
        private BudgetRepository _budgetRepo;
        private UserRepository _userRepo;

        private BackupService _backupService;

        [TestInitialize]
        public void Setup()
        {
            // 1. Configurar Base de Datos en Memoria (Una nueva para cada test)
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: System.Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            // Mock de Auth para el contexto
            var authMock = new Mock<IAuthService>();
            _context = new AppDbContext(options, authMock.Object);

            // 2. Inicializar Repositorios Reales
            _patientRepo = new PatientRepository(_context);
            _treatmentRepo = new TreatmentRepository(_context);
            _budgetRepo = new BudgetRepository(_context);
            _userRepo = new UserRepository(_context);

            // 3. Mock CryptoService (Simplificamos para que NO encripte realmente en el test)
            _cryptoServiceMock = new Mock<ICryptoService>();

            // Configuramos DecryptAsync para que simplemente copie el input al output (sin desencriptar)
            _cryptoServiceMock.Setup(c => c.DecryptAsync(It.IsAny<Stream>(), It.IsAny<Stream>(), It.IsAny<string>()))
                .Callback<Stream, Stream, string>((input, output, pass) => input.CopyTo(output))
                .Returns(Task.CompletedTask);

            // 4. Crear Servicio
            _backupService = new BackupService(
                _patientRepo,
                _treatmentRepo,
                _budgetRepo,
                _userRepo,
                _context,
                _cryptoServiceMock.Object
            );
        }

        [TestCleanup]
        public void Cleanup()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [TestMethod]
        public async Task ImportBackupAsync_DebeBorrarDatosViejos_E_InsertarNuevos()
        {
            // Arrange
            // 1. Llenar la BD actual con "Datos Viejos"
            _context.Patients.Add(new Patient { Id = 99, Name = "Viejo", Surname = "Paciente", DocumentNumber = "111" });
            _context.Treatments.Add(new Treatment { Id = 88, Name = "Tratamiento Viejo", DefaultPrice = 50 });
            await _context.SaveChangesAsync();

            // Verificar estado inicial
            Assert.AreEqual(1, _context.Patients.Count());

            // 2. Crear un JSON simulado de "Backup" con "Datos Nuevos"
            // Nota: El formato debe coincidir con la clase interna BackupData del servicio
            string jsonBackup = @"
            {
              ""Patients"": [
                { ""Id"": 1, ""Name"": ""Nuevo"", ""Surname"": ""Importado"", ""DocumentNumber"": ""222"", ""IsActive"": true }
              ],
              ""Treatments"": [
                { ""Id"": 1, ""Name"": ""Limpieza Nueva"", ""DefaultPrice"": 100, ""IsActive"": true }
              ],
              ""Users"": [], ""Budgets"": [], ""ClinicalEntries"": [], ""Payments"": [],
              ""PaymentAllocations"": [], ""Dosages"": [], ""Medications"": [], ""Prescriptions"": []
            }";

            // Convertir string a archivo temporal simulado
            string tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, jsonBackup);

            try
            {
                // Act
                bool result = await _backupService.ImportBackupAsync(tempFile, "password_dummy");

                // Assert
                Assert.IsTrue(result, "La importación debería devolver true");

                // Verificar que los datos viejos se borraron
                var pacienteViejo = await _context.Patients.FirstOrDefaultAsync(p => p.Name == "Viejo");
                Assert.IsNull(pacienteViejo, "El paciente viejo debería haber sido borrado");

                // Verificar que los datos nuevos existen
                var pacienteNuevo = await _context.Patients.FirstOrDefaultAsync(p => p.Name == "Nuevo");
                Assert.IsNotNull(pacienteNuevo, "El paciente nuevo debería existir");
                Assert.AreEqual("Importado", pacienteNuevo.Surname);

                // Verificar Tratamientos
                Assert.AreEqual(1, await _context.Treatments.CountAsync());
                Assert.AreEqual("Limpieza Nueva", (await _context.Treatments.FirstAsync()).Name);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}