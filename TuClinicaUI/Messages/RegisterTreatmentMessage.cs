using CommunityToolkit.Mvvm.Messaging.Messages;
using TuClinica.Core.Enums;

namespace TuClinica.UI.Messages
{
    /// <summary>
    /// Mensaje enviado desde OdontogramViewModel a PatientFileViewModel para registrar un cargo.
    /// </summary>
    public class RegisterTreatmentMessage
    {
        public int ToothNumber { get; }
        public ToothSurface Surface { get; }
        public ToothStatus Status { get; }
        public decimal Price { get; }

        public RegisterTreatmentMessage(int toothNumber, ToothSurface surface, ToothStatus status, decimal price)
        {
            ToothNumber = toothNumber;
            Surface = surface;
            Status = status;
            Price = price;
        }
    }
}
