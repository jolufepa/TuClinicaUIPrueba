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

        // --- 1. Propiedades para la CONDICIÓN (El color de fondo o patología) ---
        [ObservableProperty]
        private ToothCondition _fullCondition = ToothCondition.Sano; // Para estados globales
        [ObservableProperty]
        private ToothCondition _oclusalCondition = ToothCondition.Sano;
        [ObservableProperty]
        private ToothCondition _mesialCondition = ToothCondition.Sano;
        [ObservableProperty]
        private ToothCondition _distalCondition = ToothCondition.Sano;
        [ObservableProperty]
        private ToothCondition _vestibularCondition = ToothCondition.Sano;
        [ObservableProperty]
        private ToothCondition _lingualCondition = ToothCondition.Sano;

        // --- 2. Propiedades para la RESTAURACIÓN (El color del tratamiento realizado) ---
        [ObservableProperty]
        private ToothRestoration _fullRestoration = ToothRestoration.Ninguna;
        [ObservableProperty]
        private ToothRestoration _oclusalRestoration = ToothRestoration.Ninguna;
        [ObservableProperty]
        private ToothRestoration _mesialRestoration = ToothRestoration.Ninguna;
        [ObservableProperty]
        private ToothRestoration _distalRestoration = ToothRestoration.Ninguna;
        [ObservableProperty]
        private ToothRestoration _vestibularRestoration = ToothRestoration.Ninguna;
        [ObservableProperty]
        private ToothRestoration _lingualRestoration = ToothRestoration.Ninguna;

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