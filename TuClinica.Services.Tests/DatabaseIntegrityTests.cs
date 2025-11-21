using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using TuClinica.Core.Enums;
using TuClinica.DataAccess;
using TuClinica.Services.Implementation; // Necesario si usas mocks de auth, aquí usamos directo el contexto

namespace TuClinica.Services.Tests
{
    [TestClass]
    public class DatabaseIntegrityTests
    {
        [TestMethod]
        public void Initialize_DebeCrearTablasYAdmin_EnBaseDeDatosVacia()
        {
            // 1. ARRANGE (Preparación)
            // Usamos un nombre de archivo único para simular una instalación limpia
            string dbName = $"TestDb_{Guid.NewGuid()}.db";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbName}")
                .Options;

            // Necesitamos un mock o null para el AuthService ya que el constructor de AppDbContext lo pide
            // (Asumiendo que tu AppDbContext pide IAuthService, si no, pasa null)
            var authServiceMock = new Moq.Mock<TuClinica.Core.Interfaces.Services.IAuthService>();

            try
            {
                using (var context = new AppDbContext(options, authServiceMock.Object))
                {
                    // Aseguramos que NO existe la BD (simula ordenador nuevo o borrado)
                    context.Database.EnsureDeleted();

                    // 2. ACT (Ejecución de la lógica crítica)
                    var initializer = new DatabaseInitializer(context);
                    initializer.Initialize();
                }

                // 3. ASSERT (Verificación)
                // Abrimos una NUEVA conexión para verificar que los datos se persistieron en el disco
                using (var verifyContext = new AppDbContext(options, authServiceMock.Object))
                {
                    // Verificamos que podemos conectar (significa que el archivo .db se creó)
                    Assert.IsTrue(verifyContext.Database.CanConnect(), "No se pudo conectar a la BD creada.");

                    // Verificamos que existe el usuario admin (significa que la tabla Users existe y el seed funcionó)
                    var adminUser = verifyContext.Users.FirstOrDefault(u => u.Username == "admin");

                    Assert.IsNotNull(adminUser, "El usuario 'admin' no fue creado automáticamente.");
                    Assert.AreEqual(UserRole.Administrador, adminUser.Role);
                    Assert.IsTrue(adminUser.IsActive);

                    // Verificamos que la tabla PatientFiles existe (intentando consultarla)
                    // Si la tabla no existe, esto lanzará una excepción y el test fallará
                    var filesCount = verifyContext.PatientFiles.Count();
                    Assert.AreEqual(0, filesCount, "La tabla PatientFiles debería existir pero estar vacía.");
                }
            }
            finally
            {
                // Limpieza: Borrar el archivo de prueba
                if (File.Exists(dbName))
                {
                    try { File.Delete(dbName); } catch { }
                }
            }
        }
    }
}