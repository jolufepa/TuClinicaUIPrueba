using System.Windows; // Necesitamos este using para MessageBox
using TuClinica.Core.Interfaces.Services;
using TuClinica.UI.Views;
using TuClinica.Core.Enums;

namespace TuClinica.UI.Services
{
    public class DialogService : IDialogService
    {
        // Convertimos nuestro enum simple al enum de WPF
        private MessageBoxButton ConvertButtonType(DialogResult buttonType)
        {
            return buttonType switch
            {
                DialogResult.Yes => MessageBoxButton.YesNo,
                DialogResult.OK => MessageBoxButton.OK,
                _ => MessageBoxButton.OK
            };
        }

        public (bool Ok, string Password) ShowPasswordPrompt()
        {
            var dialog = new PasswordPromptDialog();

            // La implementación del servicio es responsable de encontrar la ventana principal
            if (Application.Current != null && Application.Current.MainWindow != null)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            var result = dialog.ShowDialog();

            if (result == true)
            {
                // NOTA: Asume que PasswordPromptDialog tiene una propiedad Password
                return (true, dialog.Password);
            }
            else
            {
                return (false, string.Empty);
            }
        }

        // *** CAMBIO: Implementación ELIMINADA ***
        /*
        public (bool Ok, int? TreatmentId, ToothRestoration? Restoration, decimal? Price) ShowTreatmentPriceDialog()
        {
            ...
        }
        */

        public (bool Ok, decimal Amount, string Method) ShowNewPaymentDialog()
        {
            var dialog = new NewPaymentDialog();
            if (Application.Current != null && Application.Current.MainWindow != null)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            var result = dialog.ShowDialog();

            if (result == true)
            {
                return (true, dialog.Amount, dialog.PaymentMethod);
            }
            return (false, 0, string.Empty);
        }

        public void ShowMessage(string message, string title, DialogResult buttonType = DialogResult.OK)
        {
            MessageBox.Show(message, title, ConvertButtonType(buttonType), MessageBoxImage.Information);
        }

        public DialogResult ShowConfirmation(string message, string title)
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);

            return result switch
            {
                MessageBoxResult.Yes => DialogResult.Yes,
                MessageBoxResult.No => DialogResult.No,
                _ => DialogResult.No
            };
        }
    }
}