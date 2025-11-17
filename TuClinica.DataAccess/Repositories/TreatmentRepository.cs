using Microsoft.EntityFrameworkCore; // Necesario para .Include
using System.Collections.Generic;
using System.Linq; // Necesario para .OrderBy
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Models;
using TuClinica.DataAccess;

namespace TuClinica.DataAccess.Repositories
{
    public class TreatmentRepository : Repository<Treatment>, ITreatmentRepository
    {
        public TreatmentRepository(AppDbContext context) : base(context)
        {
        }

        // Sobrescribimos GetAllAsync para incluir los hijos
        public new async Task<IEnumerable<Treatment>> GetAllAsync()
        {
            return await _context.Treatments
                .Include(t => t.PackItems)             // Carga la relación intermedia
                    .ThenInclude(pi => pi.ChildTreatment) // Carga el tratamiento hijo real
                .OrderBy(t => t.Name)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}