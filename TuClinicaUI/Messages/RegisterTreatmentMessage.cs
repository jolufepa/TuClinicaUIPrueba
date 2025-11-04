// TuClinica.UI/Messages/RegisterTreatmentMessage.cs (MODIFICADO)
using CommunityToolkit.Mvvm.Messaging.Messages;
using TuClinica.Core.Enums;
using TuClinica.UI.ViewModels;

namespace TuClinica.UI.Messages
{
    /// <summary>
    /// Mensaje enviado cuando se registra un tratamiento desde el Odontograma.
    /// Ahora incluye el ID del Treatment del catálogo.
    /// </summary>
    public class RegisterTreatmentMessage : ValueChangedMessage<int> // <--- Cambiado a 'int' (TreatmentId)
    {
        public int ToothNumber { get; }
        public ToothSurface Surface { get; }
        public decimal Price { get; }

        // Creamos una propiedad para el Restoration Type resultante (Necesario para el feedback visual instantáneo)
        public ToothRestoration RestorationResult { get; }

        public RegisterTreatmentMessage(int toothNumber, ToothSurface surface, int treatmentId, ToothRestoration restorationResult, decimal price)
            : base(treatmentId) // Valor principal es el TreatmentId
        {
            ToothNumber = toothNumber;
            Surface = surface;
            Price = price;
            RestorationResult = restorationResult;

            // Nota: Se usó 'base(treatmentId)' ya que ValueChangedMessage requiere un valor.
        }

        // El TreatmentId es accesible vía esta propiedad o a través de 'Value'.
        public int TreatmentId => Value;
    }
}