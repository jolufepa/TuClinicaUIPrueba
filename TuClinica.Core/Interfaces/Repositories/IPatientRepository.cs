using System.Collections.Generic;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Models; // Importamos nuestros Modelos

namespace TuClinica.Core.Interfaces.Repositories
{
    // Asegúrate que tu archivo se llama IPatientRepository.cs
    // y que hereda de IRepository<Patient>
    public interface IPatientRepository : IRepository<Patient>
    {
        // --- MÉTODO MODIFICADO ---
        // Añadimos 'includeInactive' para poder buscar también en archivados
        Task<IEnumerable<Patient>> SearchByNameOrDniAsync(string query, bool includeInactive = false);

        // --- MÉTODO AÑADIDO (Sobrecarga de GetAllAsync) ---
        Task<IEnumerable<Patient>> GetAllAsync(bool includeInactive = false);

        // Este es para nuestra lógica de borrado (Hard/Soft)
        Task<bool> HasHistoryAsync(int patientId);

        // Este es para el módulo de Admin (Bloque 5)
        Task<IEnumerable<Patient>> GetArchivedPatientsAsync();
    }
}