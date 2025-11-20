using System.Threading.Tasks; // Añadido
using TuClinica.Core.Models;

namespace TuClinica.Core.Interfaces.Repositories
{
    public interface ITreatmentRepository : IRepository<Treatment>
    {
        // Método nuevo para obtener un tratamiento editable con sus items incluidos
        Task<Treatment?> GetByIdWithPackItemsAsync(int id);
    }
}