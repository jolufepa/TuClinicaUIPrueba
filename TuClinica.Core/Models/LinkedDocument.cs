// En: TuClinica.Core/Models/LinkedDocument.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TuClinica.Core.Enums;

namespace TuClinica.Core.Models
{
    /// <summary>
    /// Representa un documento de identidad secundario o histórico
    /// vinculado a una ficha de paciente principal.
    /// </summary>
    public class LinkedDocument
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PatientId { get; set; }
        [ForeignKey("PatientId")]
        public Patient? Patient { get; set; }

        [Required]
        public PatientDocumentType DocumentType { get; set; }

        [Required]
        public string DocumentNumber { get; set; } = string.Empty;

        // Notas para saber qué era este documento
        public string? Notes { get; set; } // Ej: "Antiguo Pasaporte", "NIE transitorio"
    }
}