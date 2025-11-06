// En: TuClinicaUI/Services/DialogService.cs
using System.Windows;
using TuClinica.Core.Interfaces.Services;
using TuClinica.UI.Views;
using TuClinica.Core.Enums;
using System.Collections.Generic;  // <-- ¡AÑADIR ESTE USING!
using TuClinica.Core.Models;       // <-- ¡AÑADIR ESTE USING! (Para Treatment y ManualChargeResult)

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

            Window? owner = Application.Current?.MainWindow;
            if (owner != null && owner != dialog)
            {
                dialog.Owner = owner;
            }

            var result = dialog.ShowDialog();

            if (result == true)
            {
                return (true, dialog.Password);
            }
            else
            {
                return (false, string.Empty);
            }
        }


        public (bool Ok, decimal Amount, string Method) ShowNewPaymentDialog()
        {
            var dialog = new NewPaymentDialog();

            Window? owner = Application.Current?.MainWindow;
            if (owner != null && owner != dialog)
            {
                dialog.Owner = owner;
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

        // Ahora este método es válido porque los usings de arriba
        // le permiten encontrar 'ManualChargeResult' y 'Treatment'
        public (bool Ok, ManualChargeResult? Data) ShowManualChargeDialog(IEnumerable<Treatment> availableTreatments)
        {
            var dialog = new ManualChargeDialog();

            Window? owner = Application.Current.MainWindow;
            if (owner != null && owner != dialog)
            {
                dialog.Owner = owner;
            }

            dialog.AvailableTreatments = availableTreatments;

            if (dialog.ShowDialog() == true)
            {
                var resultData = new ManualChargeResult
                {
                    Concept = dialog.ManualConcept,
                    UnitPrice = dialog.UnitPrice,
                    Quantity = dialog.Quantity,
                    TreatmentId = dialog.SelectedTreatment?.Id
                };
                return (true, resultData);
            }

            return (false, null);
        }
    }
}