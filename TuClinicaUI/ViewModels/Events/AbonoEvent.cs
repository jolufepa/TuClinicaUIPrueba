using TuClinica.Core.Models;
using System;

namespace TuClinica.UI.ViewModels.Events
{
    /// <summary>
    /// Representa un Abono (Pago) en la bitácora.
    /// </summary>
    public class AbonoEvent : HistorialEventBase
    {
        public Payment Abono { get; }

        public AbonoEvent(Payment abono)
        {
            Abono = abono;
            Timestamp = abono.PaymentDate;
        }
    }
}