// En: TuClinica.Core/Interfaces/Repositories/ITreatmentPlanItemRepository.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using TuClinica.Core.Models;

namespace TuClinica.Core.Interfaces.Repositories
{
    public interface ITreatmentPlanItemRepository : IRepository<TreatmentPlanItem>
    {
        /// <summary>
        /// Obtiene todas las tareas del plan de tratamiento para un paciente específico.
        /// </summary>
        Task<IEnumerable<TreatmentPlanItem>> GetTasksForPatientAsync(int patientId);
    }
}