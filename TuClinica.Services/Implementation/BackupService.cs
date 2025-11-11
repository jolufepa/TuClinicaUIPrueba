// En: TuClinica.Services/Implementation/BackupService.cs
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.DataAccess;

namespace TuClinica.Services.Implementation
{
    public class BackupService : IBackupService
    {
        private readonly IPatientRepository _patientRepository;
        private readonly ITreatmentRepository _treatmentRepository;
        private readonly IBudgetRepository _budgetRepository;
        private readonly IUserRepository _userRepository;
        private readonly AppDbContext _context;
        private readonly ICryptoService _cryptoService;

        private class BackupData
        {
            public List<Patient> Patients { get; set; } = new();
            public List<Treatment> Treatments { get; set; } = new();
            public List<Budget> Budgets { get; set; } = new();
            public List<User> Users { get; set; } = new();
            public List<ClinicalEntry> ClinicalEntries { get; set; } = new();
            public List<Payment> Payments { get; set; } = new();
            public List<PaymentAllocation> PaymentAllocations { get; set; } = new();
            public List<Dosage> Dosages { get; set; } = new();
            public List<Medication> Medications { get; set; } = new();
            public List<Prescription> Prescriptions { get; set; } = new();
        }

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.Preserve
        };

        public BackupService(
            IPatientRepository patientRepository,
            ITreatmentRepository treatmentRepository,
            IBudgetRepository budgetRepository,
            IUserRepository userRepository,
            AppDbContext context,
            ICryptoService cryptoService)
        {
            _patientRepository = patientRepository;
            _treatmentRepository = treatmentRepository;
            _budgetRepository = budgetRepository;
            _userRepository = userRepository;
            _context = context;
            _cryptoService = cryptoService;
        }

        // --- EXPORT (REFACTORIZADO SIN AsNoTracking()) ---
        public async Task<bool> ExportBackupAsync(string filePath, string password)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            // 1. Recolectar TODOS los datos
            // --- ¡CAMBIO CRÍTICO! Eliminados todos los .AsNoTracking() ---
            // Esto es OBLIGATORIO para que EF Core resuelva las identidades
            // y el Serializador JSON cree las referencias ($ref) correctas.
            var data = new BackupData
            {
                // Tablas Raíz
                Patients = await _context.Patients.ToListAsync(),
                Treatments = await _context.Treatments.ToListAsync(),
                Users = await _context.Users.ToListAsync(),
                Dosages = await _context.Dosages.ToListAsync(),
                Medications = await _context.Medications.ToListAsync(),

                // Tablas Dependientes (Nivel 1)
                Budgets = await _context.Budgets
                    .Include(b => b.Items)
                    .Include(b => b.Patient)
                    .ToListAsync(),

                Prescriptions = await _context.Prescriptions
                    .Include(p => p.Items)
                    .Include(p => p.Patient)
                    .ToListAsync(),

                Payments = await _context.Payments
                    .Include(p => p.Patient)
                    .ToListAsync(),

                ClinicalEntries = await _context.ClinicalEntries
                    .Include(c => c.TreatmentsPerformed)
                        .ThenInclude(t => t.Treatment)
                    .Include(c => c.Patient)
                    .Include(c => c.Doctor)
                    .ToListAsync(),

                // Tablas Dependientes (Nivel 2)
                PaymentAllocations = await _context.PaymentAllocations
                    .Include(pa => pa.Payment)
                    .Include(pa => pa.ClinicalEntry)
                    .ToListAsync()
            };

            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            await using var jsonStream = new MemoryStream();

            await JsonSerializer.SerializeAsync(jsonStream, data, _jsonOptions);
            jsonStream.Seek(0, SeekOrigin.Begin);

            await _cryptoService.EncryptAsync(jsonStream, fileStream, password);

            return true;
        }

        // --- IMPORT (REFACTORIZADO PARA MANEJAR REFERENCIAS) ---
        public async Task<bool> ImportBackupAsync(string filePath, string password)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                await using var jsonStream = new MemoryStream();

                await _cryptoService.DecryptAsync(fileStream, jsonStream, password);
                jsonStream.Seek(0, SeekOrigin.Begin);

                BackupData? data = await JsonSerializer.DeserializeAsync<BackupData>(jsonStream, _jsonOptions);

                if (data == null)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                // --- 6. Borrar datos existentes (¡El orden importa!) ---
                _context.PaymentAllocations.RemoveRange(_context.PaymentAllocations);
                _context.ToothTreatments.RemoveRange(_context.ToothTreatments);
                _context.PrescriptionItems.RemoveRange(_context.PrescriptionItems);
                _context.BudgetLineItems.RemoveRange(_context.BudgetLineItems);
                await _context.SaveChangesAsync();

                _context.ClinicalEntries.RemoveRange(_context.ClinicalEntries);
                _context.Payments.RemoveRange(_context.Payments);
                _context.Prescriptions.RemoveRange(_context.Prescriptions);
                _context.Budgets.RemoveRange(_context.Budgets);
                await _context.SaveChangesAsync();

                _context.Patients.RemoveRange(_context.Patients);
                _context.Treatments.RemoveRange(_context.Treatments);
                _context.Users.RemoveRange(_context.Users);
                _context.Dosages.RemoveRange(_context.Dosages);
                _context.Medications.RemoveRange(_context.Medications);
                // ¡NO guardamos aquí!

                // --- 7. Insertar datos importados (¡CORREGIDO!) ---

                // Poner IDs a 0
                data.Users.ForEach(u => u.Id = 0);
                data.Patients.ForEach(p => p.Id = 0);
                data.Treatments.ForEach(t => t.Id = 0);
                data.Dosages.ForEach(d => d.Id = 0);
                data.Medications.ForEach(m => m.Id = 0);
                data.Budgets.ForEach(b => { b.Id = 0; b.Items?.ToList().ForEach(i => i.Id = 0); });
                data.Prescriptions.ForEach(p => { p.Id = 0; p.Items?.ToList().ForEach(i => i.Id = 0); });
                data.ClinicalEntries.ForEach(c => { c.Id = 0; c.TreatmentsPerformed?.ToList().ForEach(t => t.Id = 0); });
                data.Payments.ForEach(p => p.Id = 0);
                data.PaymentAllocations.ForEach(pa => pa.Id = 0);

                // --- CAMBIO CRÍTICO: Añadir TODO al DbContext ANTES de SaveChanges ---
                // Gracias a que la exportación fue correcta, EF Core entenderá
                // que el 'Patient' en 'data.Patients' y el 'Patient' en 'data.Budgets'
                // son el mismo objeto (Instancia A) y no dará el error "already tracked".

                await _context.Users.AddRangeAsync(data.Users);
                await _context.Patients.AddRangeAsync(data.Patients);
                await _context.Treatments.AddRangeAsync(data.Treatments);
                await _context.Dosages.AddRangeAsync(data.Dosages);
                await _context.Medications.AddRangeAsync(data.Medications);
                await _context.Budgets.AddRangeAsync(data.Budgets);
                await _context.Prescriptions.AddRangeAsync(data.Prescriptions);
                await _context.Payments.AddRangeAsync(data.Payments);
                await _context.ClinicalEntries.AddRangeAsync(data.ClinicalEntries);
                await _context.PaymentAllocations.AddRangeAsync(data.PaymentAllocations);

                // --- LLAMAR A SaveChanges UNA SOLA VEZ AL FINAL ---
                // EF Core es lo suficientemente inteligente para insertar todo
                // en el orden correcto (primero Pacientes, luego Presupuestos, etc.)
                // para satisfacer las constraints de clave foránea.
                await _context.SaveChangesAsync();

                // 8. Commit
                await transaction.CommitAsync();
                return true;
            }
            catch (JsonException jsonEx)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Error al leer el archivo de backup (JSON): {jsonEx.Message}", jsonEx);
            }
            catch (CryptographicException cryptEx)
            {
                await transaction.RollbackAsync();
                throw new Exception("Contraseña incorrecta o archivo de backup corrupto.", cryptEx);
            }
            catch (Exception ex)
            {
                // Este es el error que estás viendo (DbUpdateException)
                await transaction.RollbackAsync();
                // Lo relanzamos para que AdminViewModel lo muestre
                throw;
            }
        }
    }
}