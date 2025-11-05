using TuClinica.Core.Enums;
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

    public interface IDialogService
    {
        void ShowMessage(string message, string title, DialogResult buttonType = DialogResult.OK);
        DialogResult ShowConfirmation(string message, string title);
        (bool Ok, string Password) ShowPasswordPrompt();

        // *** CAMBIO: Método ELIMINADO ***
        // (bool Ok, int? TreatmentId, ToothRestoration? Restoration, decimal? Price) ShowTreatmentPriceDialog();

        (bool Ok, decimal Amount, string Method) ShowNewPaymentDialog();
    }
}