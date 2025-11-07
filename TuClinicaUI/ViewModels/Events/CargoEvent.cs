using CommunityToolkit.Mvvm.Input;
using TuClinica.Core.Models;
using System;

namespace TuClinica.UI.ViewModels.Events
{
    /// <summary>
    /// Representa un Cargo (Visita/Tratamiento) en la bitácora.
    /// </summary>
    public class CargoEvent : HistorialEventBase
    {
        public ClinicalEntry Cargo { get; }
        private readonly PatientFileViewModel _parentVM; // Para enlazar el comando de borrado

        public CargoEvent(ClinicalEntry cargo, PatientFileViewModel parentVM)
        {
            Cargo = cargo;
            _parentVM = parentVM;
            Timestamp = cargo.VisitDate;
        }

        // Exponemos el comando de borrado para el DataTemplate
        public IAsyncRelayCommand<ClinicalEntry> DeleteCommand => _parentVM.DeleteClinicalEntryAsyncCommand;
    }
}