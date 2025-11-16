// En: TuClinica.DataAccess/Repositories/PatientRepository.cs
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces; // Importa IPatientRepository
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Models; // Importa Patient
using TuClinica.DataAccess; // Importa AppDbContext

namespace TuClinica.DataAccess.Repositories
{
    // Hereda de Repository<Patient> para tener los métodos básicos (Add, Update, etc.)
    public class PatientRepository : Repository<Patient>, IPatientRepository
    {
        public PatientRepository(AppDbContext context) : base(context)
        {
        }

        // --- MÉTODO MODIFICADO ---
        public async new Task<IEnumerable<Patient>> GetAllAsync(bool includeInactive = false)
        {
            // Esta sobrecarga simple ahora llama a la paginada por defecto
            // (Aunque PatientsViewModel ya no la usará, es bueno mantenerla por consistencia)
            return await GetAllAsync(includeInactive, 1, int.MaxValue);
        }

        // --- NUEVA SOBRECARGA PAGINADA (Implementación de la interfaz) ---
        public async Task<IEnumerable<Patient>> GetAllAsync(bool includeInactive, int page, int pageSize)
        {
            var query = _context.Patients.AsNoTracking();

            // Si includeInactive es falso, filtramos solo por activos
            if (!includeInactive)
            {
                query = query.Where(p => p.IsActive);
            }

            // Aplicamos orden y paginación
            return await query
                .OrderBy(p => p.Surname)
                .ThenBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        // --- MÉTODO MODIFICADO ---
        public async Task<IEnumerable<Patient>> SearchByNameOrDniAsync(string query, bool includeInactive = false)
        {
            // Esta sobrecarga simple ahora llama a la paginada por defecto
            return await SearchByNameOrDniAsync(query, includeInactive, 1, int.MaxValue);
        }

        // --- NUEVA SOBRECARGA PAGINADA (Implementación de la interfaz) ---
        public async Task<IEnumerable<Patient>> SearchByNameOrDniAsync(string query, bool includeInactive, int page, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Enumerable.Empty<Patient>();
            }

            query = query.Trim().ToLower();

            var dbQuery = _context.Patients.AsNoTracking();

            // Si includeInactive es falso, filtramos solo por activos
            if (!includeInactive)
            {
                dbQuery = dbQuery.Where(p => p.IsActive);
            }

            // --- INICIO DE LA MODIFICACIÓN ---
            // Aplicamos el filtro de búsqueda de texto
            var filteredQuery = dbQuery
                .Where(p => p.Name.ToLower().Contains(query) ||
                            p.Surname.ToLower().Contains(query) ||
                            p.DocumentNumber.ToLower().Contains(query)); // <-- CAMBIADO DE DniNie
            // --- FIN DE LA MODIFICACIÓN ---

            // Aplicamos orden y paginación
            return await filteredQuery
                .OrderBy(p => p.Surname)
                .ThenBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<bool> HasHistoryAsync(int patientId)
        {
            // Nuestra lógica de si tiene historial (presupuestos)
            // --- MODIFICADO: Comprobación más robusta (incluye historial clínico) ---
            bool hasBudgets = await _context.Budgets.AnyAsync(b => b.PatientId == patientId);
            if (hasBudgets) return true;

            bool hasEntries = await _context.ClinicalEntries.AnyAsync(c => c.PatientId == patientId);
            return hasEntries;
        }

        public async Task<IEnumerable<Patient>> GetArchivedPatientsAsync()
        {
            // Para la pestaña de Utilidades: ver pacientes archivados
            return await _context.Patients
                                 .Where(p => !p.IsActive) // Filtrar por IsActive == false
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        // --- IMPLEMENTACIÓN DE MÉTODOS DE CONTEO AÑADIDOS ---

        public async Task<int> GetCountAsync(bool includeInactive)
        {
            var query = _context.Patients.AsNoTracking();
            if (!includeInactive)
            {
                query = query.Where(p => p.IsActive);
            }
            return await query.CountAsync();
        }

        public async Task<int> GetCountAsync(string query, bool includeInactive)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return await GetCountAsync(includeInactive);
            }

            query = query.Trim().ToLower();
            var dbQuery = _context.Patients.AsNoTracking();

            if (!includeInactive)
            {
                dbQuery = dbQuery.Where(p => p.IsActive);
            }

            // --- INICIO DE LA MODIFICACIÓN ---
            return await dbQuery
                .Where(p => p.Name.ToLower().Contains(query) ||
                            p.Surname.ToLower().Contains(query) ||
                            p.DocumentNumber.ToLower().Contains(query)) // <-- CAMBIADO DE DniNie
                .CountAsync();
            // --- FIN DE LA MODIFICACIÓN ---
        }
    }
}