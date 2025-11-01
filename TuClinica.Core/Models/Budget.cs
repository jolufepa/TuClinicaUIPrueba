using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TuClinica.Core.Enums;

namespace TuClinica.Core.Models
{
    public class Budget
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string BudgetNumber { get; set; } = string.Empty;

        public DateTime IssueDate { get; set; }

        public int PatientId { get; set; }
        [ForeignKey("PatientId")]
        // *** CORRECCIÓN: Añadido '?' para indicar que Patient puede ser null ***
        public Patient? Patient { get; set; } // Quitamos '= null!'

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal DiscountPercent { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal VatPercent { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalAmount { get; set; }

        // Permitir que PdfFilePath sea null si aún no se ha generado
        public string? PdfFilePath { get; set; } // Cambiado a nullable también

        public BudgetStatus Status { get; set; } = BudgetStatus.Pendiente;

        public ICollection<BudgetLineItem> Items { get; set; } = new List<BudgetLineItem>();
    }
}