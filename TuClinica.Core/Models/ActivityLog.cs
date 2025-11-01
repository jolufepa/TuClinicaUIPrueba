// En: TuClinica.Core/Models/ActivityLog.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace TuClinica.Core.Models
{
    public class ActivityLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string ActionType { get; set; } = string.Empty; // Ej: "Create", "Update", "Delete", "Access"

        [Required]
        public string EntityType { get; set; } = string.Empty; // Ej: "Patient", "Budget"

        // ID de la entidad afectada (opcional si es una acción general)
        public int? EntityId { get; set; }

        // Un campo de texto para guardar detalles, como los cambios (en formato JSON)
        public string? Details { get; set; }
    }
}