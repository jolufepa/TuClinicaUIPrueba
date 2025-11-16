// En: TuClinicaUI/Services/DialogService.cs
using System.Windows;
using TuClinica.Core.Interfaces.Services;
using TuClinica.UI.Views;
using TuClinica.Core.Enums;
using System.Collections.Generic;
using TuClinica.Core.Models;      
using Microsoft.Extensions.DependencyInjection;
using System;

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


        // --- MÉTODO MODIFICADO ---
        public (bool Ok, decimal Amount, string Method, string Observaciones, DateTime? Date) ShowNewPaymentDialog()
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
                // Devolvemos los nuevos valores
                return (true, dialog.Amount, dialog.PaymentMethod, dialog.Observaciones, dialog.SelectedDate);
            }
            // Devolvemos valores por defecto para los nuevos campos
            return (false, 0, string.Empty, string.Empty, null);
        }
        // --- FIN MÉTODO MODIFICADO ---

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
                    TreatmentId = dialog.SelectedTreatment?.Id,

                    Observaciones = dialog.Observaciones,
                    SelectedDate = dialog.SelectedDate
                    
                };
                return (true, resultData);
            }

            return (false, null);
        }
        public (bool Ok, PatientDocumentType DocumentType, string DocumentNumber, string Notes) ShowLinkedDocumentDialog()
        {
            // Usamos el ServiceProvider para crear la nueva ventana
            var dialog = App.AppHost!.Services.GetRequiredService<LinkedDocumentDialog>();

            Window? owner = Application.Current.MainWindow;
            if (owner != null && owner != dialog)
            {
                dialog.Owner = owner;
            }

            if (dialog.ShowDialog() == true)
            {
                return (true, dialog.DocumentType, dialog.DocumentNumber, dialog.Notes);
            }

            return (false, PatientDocumentType.Otro, string.Empty, string.Empty);
        }
    }
}