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
       
        // Añadimos 'page' y 'pageSize'
        Task<IEnumerable<Patient>> SearchByNameOrDniAsync(string query, bool includeInactive, int page, int pageSize);

        // --- MÉTODO MODIFICADO (Sobrecarga de GetAllAsync) ---
        // Añadimos 'page' y 'pageSize'
        Task<IEnumerable<Patient>> GetAllAsync(bool includeInactive, int page, int pageSize);

        // Este es para nuestra lógica de borrado (Hard/Soft)
        Task<bool> HasHistoryAsync(int patientId);

        // Este es para el módulo de Admin (Bloque 5)
        Task<IEnumerable<Patient>> GetArchivedPatientsAsync();

        // --- MÉTODOS AÑADIDOS ---
        // Necesitamos estos métodos para calcular el total de páginas
        Task<int> GetCountAsync(bool includeInactive);
        Task<int> GetCountAsync(string query, bool includeInactive);
    }
}