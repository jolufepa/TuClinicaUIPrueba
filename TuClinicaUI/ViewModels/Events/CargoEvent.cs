using CommunityToolkit.Mvvm.Input;
using TuClinica.Core.Models;
using System;

namespace TuClinica.UI.ViewModels.Events
{
    public class CargoEvent : HistorialEventBase
    {
        public ClinicalEntry Cargo { get; }
        // --- CAMBIO: Apunta al nuevo ViewModel Financiero ---
        private readonly PatientFinancialViewModel _parentVM;

        public CargoEvent(ClinicalEntry cargo, PatientFinancialViewModel parentVM)
        {
            Cargo = cargo;
            _parentVM = parentVM;
            Timestamp = cargo.VisitDate;
        }

        public IAsyncRelayCommand<ClinicalEntry> DeleteCommand => _parentVM.DeleteClinicalEntryAsyncCommand;
    }
}