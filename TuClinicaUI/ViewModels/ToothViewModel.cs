using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using TuClinica.Core.Enums;
using TuClinica.UI.Messages;

namespace TuClinica.UI.ViewModels
{
    public partial class ToothViewModel : ObservableObject
    {
        public int ToothNumber { get; }

        [ObservableProperty]
        private ToothStatus _fullStatus = ToothStatus.Sano;
        [ObservableProperty]
        private ToothStatus _oclusalStatus = ToothStatus.Sano;
        [ObservableProperty]
        private ToothStatus _mesialStatus = ToothStatus.Sano;
        [ObservableProperty]
        private ToothStatus _distalStatus = ToothStatus.Sano;
        [ObservableProperty]
        private ToothStatus _vestibularStatus = ToothStatus.Sano;
        [ObservableProperty]
        private ToothStatus _lingualStatus = ToothStatus.Sano;

        public ToothViewModel(int toothNumber)
        {
            ToothNumber = toothNumber;
        }

        [RelayCommand]
        private void SurfaceClick(string surfaceName)
        {
            if (Enum.TryParse<ToothSurface>(surfaceName, true, out var surface))
            {
                // Enviar mensaje al OdontogramViewModel
                WeakReferenceMessenger.Default.Send(new SurfaceClickedMessage(this, ToothNumber, surface));
            }
        }
    }
}