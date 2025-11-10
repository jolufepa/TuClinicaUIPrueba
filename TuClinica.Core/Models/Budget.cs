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
        public Patient? Patient { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal DiscountPercent { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal VatPercent { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalAmount { get; set; }

        public string? PdfFilePath { get; set; }

        public BudgetStatus Status { get; set; } = BudgetStatus.Pendiente;

        public ICollection<BudgetLineItem> Items { get; set; } = new List<BudgetLineItem>();

        // --- INICIO DE CÓDIGO AÑADIDO (FINANCIACIÓN) ---

        /// <summary>
        /// Número de plazos de financiación (0 = Pago único).
        /// </summary>
        public int NumberOfMonths { get; set; } = 0;

        /// <summary>
        /// Tasa de Interés Nominal (TIN) anual para la financiación.
        /// Ej: 5.5 para 5.5%
        /// </summary>
        [Column(TypeName = "decimal(18, 2)")]
        public decimal InterestRate { get; set; } = 0;

       
    }
}