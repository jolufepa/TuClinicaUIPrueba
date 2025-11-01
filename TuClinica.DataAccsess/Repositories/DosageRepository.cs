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
    public class DosageRepository : Repository<Dosage>, IDosageRepository
    {
        public DosageRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Dosage>> GetAllActiveAsync()
        {
            // Implementa la lógica de filtrado que estaba en el ViewModel
            return await _context.Dosages
                                 .Where(d => d.IsActive)
                                 .OrderBy(d => d.Pauta)
                                 .AsNoTracking()
                                 .ToListAsync();
        }
    }
}