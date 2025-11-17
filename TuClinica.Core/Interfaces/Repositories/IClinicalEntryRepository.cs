using System.Collections.Generic;
using System.Threading.Tasks;
using TuClinica.Core.Models;

namespace TuClinica.Core.Interfaces.Repositories
{
    public interface IClinicalEntryRepository : IRepository<ClinicalEntry>
    {
        Task<IEnumerable<ClinicalEntry>> GetHistoryForPatientAsync(int patientId);
        Task<bool> DeleteEntryAndAllocationsAsync(int entryId);

        // --- NUEVO MÉTODO ---
        /// <summary>
        /// Calcula la suma total de los cargos del paciente directamente en la BD.
        /// </summary>
        Task<decimal> GetTotalChargedForPatientAsync(int patientId);
    }
}