using System;

namespace TuClinica.Core.Models
{
    public enum TransactionType
    {
        Cargo,  // Tratamiento/Visita
        Abono   // Pago
    }

    public class FinancialTransactionDto
    {
        public DateTime Date { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TransactionType Type { get; set; }

        // Usamos 0 para indicar que no aplica, el Converter visual lo cambiará a "- €"
        public decimal ChargeAmount { get; set; }
        public decimal PaymentAmount { get; set; }
    }
}