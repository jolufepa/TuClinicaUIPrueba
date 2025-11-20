using CommunityToolkit.Mvvm.Input;
using TuClinica.Core.Models;
using System;

namespace TuClinica.UI.ViewModels.Events
{
    public class AbonoEvent : HistorialEventBase
    {
        public Payment Abono { get; }
        // --- CAMBIO: Apunta al nuevo ViewModel Financiero ---
        private readonly PatientFinancialViewModel _parentVM;

        public AbonoEvent(Payment abono, PatientFinancialViewModel parentVM)
        {
            Abono = abono;
            _parentVM = parentVM;
            Timestamp = abono.PaymentDate;
        }

        public IAsyncRelayCommand<Payment> DeleteCommand => _parentVM.DeletePaymentAsyncCommand;
    }
}