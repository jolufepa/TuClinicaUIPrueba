// En: TuClinica.Core/Models/Payment.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
// --- AÑADIR ESTE USING ---
using System.Text.Json.Serialization;

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

        // --- CAMPO AÑADIDO ---
        /// <summary>
        /// Notas u observaciones asociadas a este pago.
        /// </summary>
        public string? Observaciones { get; set; }
        // --- FIN CAMPO AÑADIDO ---

        public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();

        // --- AÑADIR JsonIgnore ---
        [NotMapped]
        [JsonIgnore]
        public decimal UnallocatedAmount => Amount - (Allocations?.Sum(a => a.AmountAllocated) ?? 0);

        // --- AÑADIR JsonIgnore ---
        [NotMapped]
        [JsonIgnore]
        public string PaymentDisplayInfo => $"{PaymentDate:dd/MM/yy} - {Method} ({UnallocatedAmount:C} restantes)";
    }
}