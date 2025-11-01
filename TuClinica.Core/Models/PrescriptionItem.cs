using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TuClinica.Core.Models
{
    public class PrescriptionItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PrescriptionId { get; set; }
        [ForeignKey("PrescriptionId")]
        public Prescription? Prescription { get; set; }

        // --- Datos del Medicamento (Snapshot) ---
        [Required]
        [MaxLength(150)]
        public string MedicationName { get; set; } // Ej: "Amoxicilina 750mg"

        [MaxLength(50)]
        public string? Quantity { get; set; } // Ej: "1 envase"

        [MaxLength(100)]
        public string? DosagePauta { get; set; } // Ej: "1 cada 8 horas"

        [MaxLength(50)]
        public string? Duration { get; set; } // Ej: "10 días"
    }
}