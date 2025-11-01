using System.Collections.Generic;
using System.Threading.Tasks;
using TuClinica.Core.Models;

namespace TuClinica.Core.Interfaces.Repositories
{
    public interface IDosageRepository : IRepository<Dosage>
    {
        // Añadimos un método específico que cargue solo los activos.
        Task<IEnumerable<Dosage>> GetAllActiveAsync();
    }
}