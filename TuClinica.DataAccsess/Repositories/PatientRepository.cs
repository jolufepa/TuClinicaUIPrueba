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

        public async new Task<IEnumerable<Patient>> GetAllAsync()
        {
            // Sobreescribimos GetAllAsync para que SOLO nos devuelva pacientes activos
            return await _context.Patients
                                 .Where(p => p.IsActive) // Filtrar por IsActive == true
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<IEnumerable<Patient>> SearchByNameOrDniAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Enumerable.Empty<Patient>();
            }

            query = query.Trim().ToLower();

            return await _context.Patients
                                 .Where(p => p.IsActive && // Solo activos
                                             (p.Name.ToLower().Contains(query) ||
                                              p.Surname.ToLower().Contains(query) ||
                                              p.DniNie.ToLower().Contains(query)))
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<bool> HasHistoryAsync(int patientId)
        {
            // Nuestra lógica de si tiene historial (presupuestos)
            return await _context.Budgets.AnyAsync(b => b.PatientId == patientId);
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