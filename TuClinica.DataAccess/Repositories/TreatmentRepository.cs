using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
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

        // Sobrescribimos GetAllAsync para incluir los hijos (Solo lectura, AsNoTracking)
        public new async Task<IEnumerable<Treatment>> GetAllAsync()
        {
            return await _context.Treatments
                .Include(t => t.PackItems)
                    .ThenInclude(pi => pi.ChildTreatment)
                .OrderBy(t => t.Name)
                .AsNoTracking() // <--- Esto es bueno para listas, pero malo para editar
                .ToListAsync();
        }

        // --- IMPLEMENTACIÓN NUEVA ---
        public async Task<Treatment?> GetByIdWithPackItemsAsync(int id)
        {
            // Aquí NO usamos AsNoTracking porque queremos editar esta entidad
            return await _context.Treatments
                .Include(t => t.PackItems)
                // No incluimos ChildTreatment anidado profundamente porque solo necesitamos los IDs para la lógica de guardado,
                // pero si lo necesitas visualmente al depurar, puedes dejarlo.
                .FirstOrDefaultAsync(t => t.Id == id);
        }
    }
}