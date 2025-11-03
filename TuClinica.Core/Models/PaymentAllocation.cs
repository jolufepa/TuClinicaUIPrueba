using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TuClinica.Core.Models
{
    /// <summary>
    /// Tabla "puente" que asigna un Abono a un Cargo.
    /// </summary>
    public class PaymentAllocation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PaymentId { get; set; }
        [ForeignKey("PaymentId")]
        public Payment? Payment { get; set; }

        [Required]
        public int ClinicalEntryId { get; set; }
        [ForeignKey("ClinicalEntryId")]
        public ClinicalEntry? ClinicalEntry { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal AmountAllocated { get; set; }
    }
}
