using CommunityToolkit.Mvvm.Messaging.Messages;
using TuClinica.Core.Models;

namespace TuClinica.UI.Messages
{
    /// <summary>
    /// Mensaje que se envía para navegar a la vista de presupuestos
    /// y opcionalmente pre-cargar un paciente.
    /// </summary>
    public class NavigateToNewBudgetMessage : ValueChangedMessage<Patient>
    {
        public NavigateToNewBudgetMessage(Patient patient) : base(patient)
        {
        }
    }
}