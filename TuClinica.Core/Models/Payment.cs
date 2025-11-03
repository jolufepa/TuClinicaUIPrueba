using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace TuClinica.Core.Models
{
    /// <summary>
    /// Representa el "Abono" o transacción financiera.
    /// </summary>
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PatientId { get; set; }
        [ForeignKey("PatientId")]
        public Patient? Patient { get; set; }

        public DateTime PaymentDate { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }
        public string? Method { get; set; } // Ej: "Efectivo", "Tarjeta"

        public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();

        [NotMapped]
        public decimal UnallocatedAmount => Amount - (Allocations?.Sum(a => a.AmountAllocated) ?? 0);
    }
}