using Microsoft.EntityFrameworkCore; // For transactions and ToListAsync
using System;
using System.Collections.Generic; // For List
using System.IO;
using System.Linq;
using System.Security.Cryptography; // Para CryptographicException
using System.Text; // For Encoding
using System.Text.Json; // For JSON serialization
using System.Text.Json.Serialization; // For ReferenceHandler
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models; // Need all models
using TuClinica.DataAccess; // For AppDbContext

namespace TuClinica.Services.Implementation
{
    public class BackupService : IBackupService
    {
        // Inject all repositories and the DbContext
        private readonly IPatientRepository _patientRepository;
        private readonly ITreatmentRepository _treatmentRepository;
        private readonly IBudgetRepository _budgetRepository;
        private readonly IUserRepository _userRepository;
        private readonly AppDbContext _context; // Needed for transaction
        private readonly ICryptoService _cryptoService; // Dependencia del servicio de cripto

        // Simple structure to hold all backup data
        private class BackupData
        {
            public List<Patient> Patients { get; set; } = new();
            public List<Treatment> Treatments { get; set; } = new();
            public List<Budget> Budgets { get; set; } = new();
            public List<User> Users { get; set; } = new();
        }

        public BackupService(
            IPatientRepository patientRepository,
            ITreatmentRepository treatmentRepository,
            IBudgetRepository budgetRepository,
            IUserRepository userRepository,
            AppDbContext context,
            ICryptoService cryptoService) // Inyección del servicio de cripto
        {
            _patientRepository = patientRepository;
            _treatmentRepository = treatmentRepository;
            _budgetRepository = budgetRepository;
            _userRepository = userRepository;
            _context = context;
            _cryptoService = cryptoService; // Asignación
        }

        // --- EXPORT ---
        public async Task<bool> ExportBackupAsync(string filePath, string password)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            try
            {
                // 1. Gather all data
                var data = new BackupData
                {
                    // Fetch data including necessary navigation properties for serialization
                    Patients = await _context.Patients.ToListAsync(),
                    Treatments = await _context.Treatments.ToListAsync(),
                    Budgets = await _context.Budgets.Include(b => b.Items).ToListAsync(), // Include LineItems
                    Users = await _context.Users.ToListAsync()
                };

                // 2. Serialize to JSON with cycle handling

                // ¡¡AQUÍ ESTABA EL ERROR CS0841!! Esta es la línea que faltaba.
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true, // Optional: for readability
                    ReferenceHandler = ReferenceHandler.Preserve // Handle object cycles
                };

                string jsonData = JsonSerializer.Serialize(data, options);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonData);

                // 3. Encrypt JSON data (usando el servicio)
                byte[] encryptedBytes = _cryptoService.Encrypt(jsonBytes, password);

                // 4. Write encrypted data to file
                await File.WriteAllBytesAsync(filePath, encryptedBytes);

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        // --- IMPORT ---
        public async Task<bool> ImportBackupAsync(string filePath, string password)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Read encrypted data
                byte[] encryptedBytes = await File.ReadAllBytesAsync(filePath);

                // 2. Decrypt data (usando el servicio)
                byte[]? jsonBytes = _cryptoService.Decrypt(encryptedBytes, password);
                if (jsonBytes == null) { await transaction.RollbackAsync(); return false; } // Decryption failed

                // 3. Deserialize JSON with cycle handling
                string jsonData = Encoding.UTF8.GetString(jsonBytes);
                System.Diagnostics.Debug.WriteLine("--- DECRYPTED JSON DATA ---");
                System.Diagnostics.Debug.WriteLine(jsonData);
                System.Diagnostics.Debug.WriteLine("--- END JSON DATA ---");

                var options = new JsonSerializerOptions
                {
                    ReferenceHandler = ReferenceHandler.Preserve // Match export setting
                };
                BackupData? data = JsonSerializer.Deserialize<BackupData>(jsonData, options);

                if (data == null) { await transaction.RollbackAsync(); return false; } // Deserialization failed

                // --- 4. Clear existing data (Order matters!) ---
                _context.BudgetLineItems.RemoveRange(_context.BudgetLineItems);
                await _context.SaveChangesAsync();
                _context.Budgets.RemoveRange(_context.Budgets);
                await _context.SaveChangesAsync();
                _context.Treatments.RemoveRange(_context.Treatments);
                await _context.SaveChangesAsync();
                _context.Patients.RemoveRange(_context.Patients);
                await _context.SaveChangesAsync();
                _context.Users.RemoveRange(_context.Users); // Consider implications for admin users
                await _context.SaveChangesAsync();

                // --- 5. Insert imported data ---
                data.Users.ForEach(u => u.Id = 0);
                data.Patients.ForEach(p => p.Id = 0);
                data.Treatments.ForEach(t => t.Id = 0);
                data.Budgets.ForEach(b => {
                    b.Id = 0;
                    b.Items?.ToList().ForEach(i => i.Id = 0);
                });

                // Añadir entidades (ahora con Id=0)
                _context.Users.AddRange(data.Users);
                await _context.SaveChangesAsync();
                _context.Patients.AddRange(data.Patients);
                await _context.SaveChangesAsync();
                _context.Treatments.AddRange(data.Treatments);
                await _context.SaveChangesAsync();
                _context.Budgets.AddRange(data.Budgets);
                await _context.SaveChangesAsync();

                // 6. Commit transaction
                await transaction.CommitAsync();
                return true;
            }
            catch (JsonException jsonEx)
            {
                await transaction.RollbackAsync();
                return false;
            }
            catch (CryptographicException cryptEx) // Includes AuthenticationTagMismatchException
            {
                await transaction.RollbackAsync();
                return false;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return false;
            }
        }


        // --- LOS MÉTODOS 'Encrypt' y 'Decrypt' PRIVADOS HAN SIDO ELIMINADOS ---
        // (Ahora viven en CryptoService)
    }
}