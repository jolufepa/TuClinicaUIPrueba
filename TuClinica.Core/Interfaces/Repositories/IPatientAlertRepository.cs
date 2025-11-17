using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TuClinica.Core.Models;

namespace TuClinica.Core.Interfaces.Repositories
{
    public interface IPatientAlertRepository : IRepository<PatientAlert>
    {
        /// <summary>
        /// Obtiene todas las alertas activas para un paciente específico.
        /// </summary>
        Task<IEnumerable<PatientAlert>> GetActiveAlertsForPatientAsync(int patientId, CancellationToken token = default);
    }
}