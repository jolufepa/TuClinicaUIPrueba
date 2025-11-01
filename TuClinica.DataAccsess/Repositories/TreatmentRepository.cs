using TuClinica.Core.Interfaces.Repositories; // Importa ITreatmentRepository
using TuClinica.Core.Models; // Importa Treatment
using TuClinica.DataAccess; // Importa AppDbContext

namespace TuClinica.DataAccess.Repositories
{
    // Hereda de Repository<Treatment> e implementa ITreatmentRepository
    public class TreatmentRepository : Repository<Treatment>, ITreatmentRepository
    {
        public TreatmentRepository(AppDbContext context) : base(context)
        {
            // No necesitamos lógica extra por ahora, la clase base es suficiente
        }

        // Podríamos sobreescribir GetAllAsync si quisiéramos filtrar por IsActive,
        // pero por ahora mostraremos todos (incluidos los inactivos) en la gestión.
        // Si quieres filtrarlos, añade aquí el mismo GetAllAsync que en PatientRepository
        // pero usando _context.Treatments y el filtro IsActive.
    }
}