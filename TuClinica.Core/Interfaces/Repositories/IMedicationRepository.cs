using System.Collections.Generic;
using System.Threading.Tasks;
using TuClinica.Core.Models;

namespace TuClinica.Core.Interfaces.Repositories
{
    public interface IMedicationRepository : IRepository<Medication>
    {
        // Añadimos un método específico que cargue solo los activos,
        // ya que la lógica actual en el ViewModel filtra por IsActive.
        Task<IEnumerable<Medication>> GetAllActiveAsync();
    }
}