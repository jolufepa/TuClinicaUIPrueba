using System;
using System.Collections.Generic; // Para IEnumerable si fuera necesario
using System.IO; // <--- ESTA ES LA LÍNEA QUE FALTABA
using System.Linq; // Para OrderBy
using System.Text;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;

namespace TuClinica.Services.Implementation
{
    public class ActivityLogService : IActivityLogService
    {
        private readonly IRepository<ActivityLog> _logRepository;
        private readonly IAuthService _authService;

        public ActivityLogService(IRepository<ActivityLog> logRepository, IAuthService authService)
        {
            _logRepository = logRepository;
            _authService = authService;
        }

        private async Task LogAsync(string actionType, string entityType, int? entityId, string details)
        {
            var username = _authService.CurrentUser?.Username ?? "Anonymous";

            var log = new ActivityLog
            {
                Timestamp = DateTime.UtcNow,
                Username = username,
                ActionType = actionType,
                EntityType = entityType,
                EntityId = entityId,
                Details = details
            };

            await _logRepository.AddAsync(log);
            await _logRepository.SaveChangesAsync();
        }

        public async Task LogAccessAsync(string details)
        {
            await LogAsync("Access", "Application", null, details);
        }

        public async Task LogAccessAsync(string entityType, int entityId, string details)
        {
            await LogAsync("Access", entityType, entityId, details);
        }

        public async Task<string> ExportLogsAsCsvAsync(string filePath)
        {
            // 1. Obtiene todos los logs
            var logs = await _logRepository.GetAllAsync();
            var sb = new StringBuilder();

            // 2. Añade la cabecera del CSV
            sb.AppendLine("Timestamp(UTC);Username;ActionType;EntityType;EntityId;Details");

            // 3. Recorre cada log y añade una fila
            foreach (var log in logs.OrderBy(l => l.Timestamp))
            {
                // Limpia los detalles para evitar errores en el CSV
                var details = (log.Details ?? string.Empty)
                                .Replace("\"", "'")
                                .Replace(Environment.NewLine, " ");

                sb.AppendLine($"{log.Timestamp:yyyy-MM-dd HH:mm:ss};{log.Username};{log.ActionType};{log.EntityType};{log.EntityId};\"{details}\"");
            }

            // 4. Escribe el contenido al archivo
            // Ahora 'File' funcionará porque hemos añadido 'using System.IO;' arriba
            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
            return filePath;
        }

        public async Task<int> PurgeOldLogsAsync(DateTime retentionDateLimit)
        {
            // 1. Encuentra logs más antiguos que la fecha límite
            var logsToDelete = await _logRepository.FindAsync(log => log.Timestamp < retentionDateLimit);

            if (logsToDelete.Any())
            {
                // 2. Los elimina del DbContext
                _logRepository.RemoveRange(logsToDelete);

                // 3. Guarda los cambios en la BD
                return await _logRepository.SaveChangesAsync();
            }
            return 0; // No se borró nada
        }
    }
}