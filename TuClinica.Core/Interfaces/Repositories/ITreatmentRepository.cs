using TuClinica.Core.Models; // Necesitamos el modelo Treatment

namespace TuClinica.Core.Interfaces.Repositories
{
    // Hereda de IRepository<Treatment> para tener los métodos básicos
    public interface ITreatmentRepository : IRepository<Treatment>
    {
        // No necesitamos métodos específicos por ahora
    }
}
