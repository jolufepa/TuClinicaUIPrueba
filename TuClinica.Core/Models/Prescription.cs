using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TuClinica.Core.Models
{
    public class Prescription
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PatientId { get; set; }
        [ForeignKey("PatientId")]
        public Patient? Patient { get; set; } // Vinculado al paciente

        public DateTime IssueDate { get; set; } // Fecha de prescripción

        // --- Datos del Prescriptor (Snapshot) ---
        [MaxLength(100)]
        public string? PrescriptorName { get; set; } // Ej: "Dra. Elisa Pérez"
        [MaxLength(50)]
        public string? PrescriptorCollegeNum { get; set; } // Ej: "Col. 12345"
        [MaxLength(100)]
        public string? PrescriptorSpecialty { get; set; } // Ej: "Odontóloga"

        // --- Indicaciones Adicionales ---
        [MaxLength(500)]
        public string? Instructions { get; set; } // Ej: "Tomar después de las comidas"

        // Relación con las líneas de la receta
        public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
    }
}