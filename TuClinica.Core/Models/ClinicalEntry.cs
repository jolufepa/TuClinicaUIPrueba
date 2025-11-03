using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace TuClinica.Core.Models
{
    /// <summary>
    /// Representa el "Cargo" o la visita clínica.
    /// </summary>
    public class ClinicalEntry
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PatientId { get; set; }
        [ForeignKey("PatientId")]
        public Patient? Patient { get; set; }

        [Required]
        public int DoctorId { get; set; }
        [ForeignKey("DoctorId")]
        public User? Doctor { get; set; }

        public DateTime VisitDate { get; set; }
        public string? Diagnosis { get; set; }
        public string? Notes { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalCost { get; set; }

        public ICollection<ToothTreatment> TreatmentsPerformed { get; set; } = new List<ToothTreatment>();
        public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();

        [NotMapped]
        public decimal Balance => TotalCost - (Allocations?.Sum(a => a.AmountAllocated) ?? 0);
    }
}