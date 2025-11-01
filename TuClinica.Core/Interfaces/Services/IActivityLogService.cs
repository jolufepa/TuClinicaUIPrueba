// En: TuClinica.Core/Interfaces/Services/IActivityLogService.cs
using System.Threading.Tasks;

namespace TuClinica.Core.Interfaces.Services
{
    public interface IActivityLogService
    {
        // Para acciones generales, ej: "Usuario X vio la lista de pacientes"
        Task LogAccessAsync(string details);

        // Para acciones específicas, ej: "Usuario X vio la ficha del Paciente Y"
        Task LogAccessAsync(string entityType, int entityId, string details);
        Task<int> PurgeOldLogsAsync(DateTime retentionDateLimit);

        // Define la tarea de exportar a CSV
        Task<string> ExportLogsAsCsvAsync(string filePath);
    }
}
