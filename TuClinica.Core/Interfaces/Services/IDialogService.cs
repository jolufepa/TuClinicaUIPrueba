// En: TuClinica.Core/Interfaces/Services/IDialogService.cs
using TuClinica.Core.Enums;
using System.Collections.Generic; // <-- AÑADIR ESTE USING
using TuClinica.Core.Models;     // <-- AÑADIR ESTE USING

namespace TuClinica.Core.Interfaces.Services
{
    // Usaremos un enum simple para no depender de WPF en nuestro Core
    public enum DialogResult
    {
        Yes,
        No,
        OK,
        Cancel
    }

    // --- ¡¡AQUÍ ESTÁ LA DEFINICIÓN DE LA CLASE QUE FALTA!! ---
    // (Añádela aquí, dentro del namespace pero fuera de la interfaz)
    public class ManualChargeResult
    {
        public string Concept { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public int? TreatmentId { get; set; }
    }
    // --- FIN DE LA DEFINICIÓN ---

    public interface IDialogService
    {
        void ShowMessage(string message, string title, DialogResult buttonType = DialogResult.OK);
        DialogResult ShowConfirmation(string message, string title);
        (bool Ok, string Password) ShowPasswordPrompt();

        (bool Ok, decimal Amount, string Method) ShowNewPaymentDialog();

        // Ahora este método es válido porque ManualChargeResult está definido arriba
        (bool Ok, ManualChargeResult? Data) ShowManualChargeDialog(IEnumerable<Treatment> availableTreatments);
    }
}