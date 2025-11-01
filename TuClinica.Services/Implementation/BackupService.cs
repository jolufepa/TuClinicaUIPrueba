using Microsoft.EntityFrameworkCore; // For transactions and ToListAsync
using System;
using System.Collections.Generic; // For List
using System.IO;
using System.Linq;
using System.Security.Cryptography; // For AES, Rfc2898DeriveBytes, RandomNumberGenerator
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
            AppDbContext context)
        {
            _patientRepository = patientRepository;
            _treatmentRepository = treatmentRepository;
            _budgetRepository = budgetRepository;
            _userRepository = userRepository;
            _context = context;
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
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true, // Optional: for readability
                    ReferenceHandler = ReferenceHandler.Preserve // Handle object cycles
                };
                string jsonData = JsonSerializer.Serialize(data, options);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonData);

                // 3. Encrypt JSON data
                byte[] encryptedBytes = Encrypt(jsonBytes, password);

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

                // 2. Decrypt data
                byte[]? jsonBytes = Decrypt(encryptedBytes, password);
                if (jsonBytes == null) { await transaction.RollbackAsync(); return false; } // Decryption failed

                // 3. Deserialize JSON with cycle handling
                string jsonData = Encoding.UTF8.GetString(jsonBytes);
                System.Diagnostics.Debug.WriteLine("--- DECRYPTED JSON DATA ---"); // Debug
                System.Diagnostics.Debug.WriteLine(jsonData);                     // Debug
                System.Diagnostics.Debug.WriteLine("--- END JSON DATA ---");     // Debug

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
                // *** ANTES de AddRange, resetear IDs para que EF Core/SQLite los genere ***
                data.Users.ForEach(u => u.Id = 0);
                data.Patients.ForEach(p => p.Id = 0);
                data.Treatments.ForEach(t => t.Id = 0);
                data.Budgets.ForEach(b => {
                    b.Id = 0;
                    // También resetear IDs de los Items si existen y tienen ID propio
                    b.Items?.ToList().ForEach(i => i.Id = 0);
                });

                // Añadir entidades (ahora con Id=0)
                _context.Users.AddRange(data.Users);
                await _context.SaveChangesAsync();
                _context.Patients.AddRange(data.Patients);
                await _context.SaveChangesAsync();
                _context.Treatments.AddRange(data.Treatments);
                await _context.SaveChangesAsync();
                _context.Budgets.AddRange(data.Budgets); // EF debería manejar los Items relacionados
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


        // --- AES Encryption/Decryption Helpers ---

        private byte[] Encrypt(byte[] dataToEncrypt, string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            using var keyDerivation = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
            byte[] key = keyDerivation.GetBytes(32); // AES-256
            byte[] nonce = RandomNumberGenerator.GetBytes(12); // AES-GCM nonce
            using var aesGcm = new AesGcm(key);
            byte[] cipherText = new byte[dataToEncrypt.Length];
            byte[] tag = new byte[16]; // GCM Auth Tag
            aesGcm.Encrypt(nonce, dataToEncrypt, cipherText, tag);

            // Combine: [salt][nonce][tag][ciphertext]
            byte[] encryptedDataWithMeta = new byte[salt.Length + nonce.Length + tag.Length + cipherText.Length];
            Buffer.BlockCopy(salt, 0, encryptedDataWithMeta, 0, salt.Length);
            Buffer.BlockCopy(nonce, 0, encryptedDataWithMeta, salt.Length, nonce.Length);
            Buffer.BlockCopy(tag, 0, encryptedDataWithMeta, salt.Length + nonce.Length, tag.Length);
            Buffer.BlockCopy(cipherText, 0, encryptedDataWithMeta, salt.Length + nonce.Length + tag.Length, cipherText.Length);
            return encryptedDataWithMeta;
        }

        private byte[]? Decrypt(byte[] encryptedDataWithMeta, string password)
        {
            const int saltSize = 16; const int nonceSize = 12; const int tagSize = 16;
            int expectedMinLength = saltSize + nonceSize + tagSize;

            if (encryptedDataWithMeta == null || encryptedDataWithMeta.Length < expectedMinLength)
            {
                return null;
            }

            try
            {
                // Extract metadata
                byte[] salt = new byte[saltSize]; byte[] nonce = new byte[nonceSize]; byte[] tag = new byte[tagSize];
                int cipherTextLength = encryptedDataWithMeta.Length - expectedMinLength;
                byte[] cipherText = new byte[cipherTextLength];
                Buffer.BlockCopy(encryptedDataWithMeta, 0, salt, 0, saltSize);
                Buffer.BlockCopy(encryptedDataWithMeta, saltSize, nonce, 0, nonceSize);
                Buffer.BlockCopy(encryptedDataWithMeta, saltSize + nonceSize, tag, 0, tagSize);
                Buffer.BlockCopy(encryptedDataWithMeta, expectedMinLength, cipherText, 0, cipherTextLength);

                // Derive key
                using var keyDerivation = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
                byte[] key = keyDerivation.GetBytes(32);

                // Decrypt
                using var aesGcm = new AesGcm(key);
                byte[] decryptedData = new byte[cipherText.Length];
                aesGcm.Decrypt(nonce, cipherText, tag, decryptedData);
                return decryptedData;
            }
            catch (CryptographicException ex)
            {
                return null;
            }
            catch (Exception ex)
            {
                return null; // Return null on unexpected errors too
            }
        }
    }
}