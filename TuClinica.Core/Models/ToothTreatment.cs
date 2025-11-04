using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TuClinica.Core.Enums;

namespace TuClinica.Core.Models
{
    /// <summary>
    /// Representa el "Acto" clínico realizado en un diente.
    /// </summary>
    public class ToothTreatment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClinicalEntryId { get; set; }
        [ForeignKey("ClinicalEntryId")]
        public ClinicalEntry? ClinicalEntry { get; set; }

        public int ToothNumber { get; set; } // Ej: 11, 12, ... 48
        
        public ToothRestoration TreatmentPerformed { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Price { get; set; }

        public int TreatmentId { get; set; }
        [ForeignKey("TreatmentId")]
        public Treatment? Treatment { get; set; } // Referencia al catálogo de tratamientos

        // ToothSurface se mantiene como Flags.
        public ToothSurface Surfaces { get; set; }
    }
}