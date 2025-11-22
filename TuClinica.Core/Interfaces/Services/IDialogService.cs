using TuClinica.Core.Enums;
using System.Collections.Generic;
using TuClinica.Core.Models;
using System;

namespace TuClinica.Core.Interfaces.Services
{
    public enum DialogResult
    {
        Yes,
        No,
        OK,
        Cancel
    }

    public class ManualChargeResult
    {
        public string Concept { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public int? TreatmentId { get; set; }

        public string Observaciones { get; set; } = string.Empty;
        public DateTime? SelectedDate { get; set; }
    }

    public interface IDialogService
    {
        void ShowMessage(string message, string title, DialogResult buttonType = DialogResult.OK);
        DialogResult ShowConfirmation(string message, string title);
        (bool Ok, string Password) ShowPasswordPrompt();
        (bool Ok, DateTime Start, DateTime End) ShowTimeSelectionDialog();
        (bool Ok, string FileName, FileCategory Category) ShowDocumentDetailsDialog(string defaultName);

        (bool Ok, decimal Amount, string Method, string Observaciones, DateTime? Date) ShowNewPaymentDialog();

        (bool Ok, ManualChargeResult? Data) ShowManualChargeDialog(IEnumerable<Treatment> availableTreatments);
        (bool Ok, PatientDocumentType DocumentType, string DocumentNumber, string Notes) ShowLinkedDocumentDialog();
    }
}