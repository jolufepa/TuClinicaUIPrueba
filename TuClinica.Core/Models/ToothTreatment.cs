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
        public ToothSurface Surfaces { get; set; }
        public ToothStatus TreatmentPerformed { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Price { get; set; }
    }
}