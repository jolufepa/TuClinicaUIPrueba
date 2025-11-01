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
        // Este es el nombre correcto que SÍ tienes en tu clase
        Task<IEnumerable<Patient>> SearchByNameOrDniAsync(string query);

        // Este método SÍ lo tienes en la clase (Repository.cs)
        // pero debe estar aquí para que GetAllAsync() funcione
       

        // Este es para nuestra lógica de borrado (Hard/Soft)
        Task<bool> HasHistoryAsync(int patientId);

        // Este es para el módulo de Admin (Bloque 5)
        Task<IEnumerable<Patient>> GetArchivedPatientsAsync();
    }
}