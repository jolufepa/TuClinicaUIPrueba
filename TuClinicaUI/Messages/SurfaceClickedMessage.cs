using CommunityToolkit.Mvvm.Messaging.Messages;
using TuClinica.Core.Enums;
using TuClinica.UI.ViewModels;

namespace TuClinica.UI.Messages
{
    /// <summary>
    /// Mensaje enviado desde un ToothViewModel cuando se hace clic en una superficie.
    /// </summary>
    public class SurfaceClickedMessage : ValueChangedMessage<ToothSurface>
    {
        public ToothViewModel Sender { get; }
        public int ToothNumber { get; }

        public SurfaceClickedMessage(ToothViewModel sender, int toothNumber, ToothSurface surface) : base(surface)
        {
            Sender = sender;
            ToothNumber = toothNumber;
        }
    }
}