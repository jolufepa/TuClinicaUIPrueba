// En: TuClinica.Core/Interfaces/Services/IDialogService.cs
using TuClinica.Core.Enums;
using System.Collections.Generic;
using TuClinica.Core.Models;     
using System;

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

        // --- CAMPOS AÑADIDOS ---
        public string Observaciones { get; set; } = string.Empty;
        public DateTime? SelectedDate { get; set; }
        // --- FIN CAMPOS AÑADIDOS ---
    }
    // --- FIN DE LA DEFINICIÓN ---

    public interface IDialogService
    {
        void ShowMessage(string message, string title, DialogResult buttonType = DialogResult.OK);
        DialogResult ShowConfirmation(string message, string title);
        (bool Ok, string Password) ShowPasswordPrompt();

        // --- FIRMA MODIFICADA ---
        (bool Ok, decimal Amount, string Method, string Observaciones, DateTime? Date) ShowNewPaymentDialog();
        // --- FIN FIRMA MODIFICADA ---

        // Ahora este método es válido porque ManualChargeResult está definido arriba
        (bool Ok, ManualChargeResult? Data) ShowManualChargeDialog(IEnumerable<Treatment> availableTreatments);
        (bool Ok, PatientDocumentType DocumentType, string DocumentNumber, string Notes) ShowLinkedDocumentDialog();
    }
}