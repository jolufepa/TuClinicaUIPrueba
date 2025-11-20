using CommunityToolkit.Mvvm.Input;
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
        private readonly PatientFileViewModel _parentVM; // Referencia al VM padre

        public AbonoEvent(Payment abono, PatientFileViewModel parentVM)
        {
            Abono = abono;
            _parentVM = parentVM; // Guardamos la referencia
            Timestamp = abono.PaymentDate;
        }

        // Exponemos el comando para eliminar el pago
        public IAsyncRelayCommand<Payment> DeleteCommand => _parentVM.DeletePaymentAsyncCommand;
    }
}