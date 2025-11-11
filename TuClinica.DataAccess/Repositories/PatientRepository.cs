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
            var query = _context.Patients.AsNoTracking();

            // Si includeInactive es falso, filtramos solo por activos
            if (!includeInactive)
            {
                query = query.Where(p => p.IsActive);
            }

            return await query.ToListAsync();
        }

        // --- MÉTODO MODIFICADO ---
        public async Task<IEnumerable<Patient>> SearchByNameOrDniAsync(string query, bool includeInactive = false)
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

            // Aplicamos el filtro de búsqueda de texto
            return await dbQuery
                .Where(p => p.Name.ToLower().Contains(query) ||
                            p.Surname.ToLower().Contains(query) ||
                            p.DniNie.ToLower().Contains(query))
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
    }
}