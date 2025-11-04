using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Models;
using TuClinica.DataAccess;

namespace TuClinica.DataAccess.Repositories
{
    // Hereda del repositorio genérico e implementa la interfaz específica
    public class MedicationRepository : Repository<Medication>, IMedicationRepository
    {
        public MedicationRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Medication>> GetAllActiveAsync()
        {
            // Implementa la lógica de filtrado que estaba en el ViewModel
            return await _context.Medications
                                 .Where(m => m.IsActive)
                                 .OrderBy(m => m.Name)
                                 .AsNoTracking()
                                 .ToListAsync();
        }
    }
}