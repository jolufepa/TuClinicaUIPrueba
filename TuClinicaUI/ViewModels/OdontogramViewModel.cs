using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Linq;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces.Services;
using TuClinica.UI.Messages;

namespace TuClinica.UI.ViewModels
{
    /// <summary>
    /// ViewModel para la VENTANA MODAL del odontograma interactivo.
    /// </summary>
    public partial class OdontogramViewModel : BaseViewModel, IRecipient<SurfaceClickedMessage>
    {
        private readonly IDialogService _dialogService;
        // private readonly IPdfService _pdfService; // Descomentar si implementas impresión

        public ObservableCollection<ToothViewModel> Odontogram { get; } = new();

        public OdontogramViewModel(IDialogService dialogService/*, IPdfService pdfService*/)
        {
            _dialogService = dialogService;
            // _pdfService = pdfService;

            // Escuchar clics desde los ToothViewModels
            WeakReferenceMessenger.Default.Register<SurfaceClickedMessage>(this);
        }

        /// <summary>
        /// Carga el estado actual del odontograma maestro (desde PatientFileViewModel).
        /// </summary>
        public void LoadState(ObservableCollection<ToothViewModel> masterOdontogram)
        {
            Odontogram.Clear();
            foreach (var tooth in masterOdontogram)
            {
                // Copiamos el estado para no modificar el maestro directamente
                var copy = new ToothViewModel(tooth.ToothNumber)
                {
                    FullStatus = tooth.FullStatus,
                    OclusalStatus = tooth.OclusalStatus,
                    MesialStatus = tooth.MesialStatus,
                    DistalStatus = tooth.DistalStatus,
                    VestibularStatus = tooth.VestibularStatus,
                    LingualStatus = tooth.LingualStatus
                };
                Odontogram.Add(copy);
            }
        }

        /// <summary>
        /// Recibe el clic de un ToothViewModel.
        /// </summary>
        public void Receive(SurfaceClickedMessage message)
        {
            // 1. Abrir diálogo para preguntar qué tratamiento y precio
            var (ok, status, price) = _dialogService.ShowTreatmentPriceDialog();

            if (ok && status.HasValue)
            {
                // 2. Enviar mensaje al PatientFileViewModel para que registre el cargo en la BD
                WeakReferenceMessenger.Default.Send(new RegisterTreatmentMessage(
                    message.ToothNumber,
                    message.Value, // La superficie
                    status.Value,  // El tratamiento
                    price
                ));

                // 3. Actualizar nuestra UI local para feedback instantáneo
                var toothToUpdate = Odontogram.FirstOrDefault(t => t.ToothNumber == message.ToothNumber);
                if (toothToUpdate != null)
                {
                    UpdateToothSurface(toothToUpdate, message.Value, status.Value);
                }
            }
        }

        private void UpdateToothSurface(ToothViewModel tooth, ToothSurface surface, ToothStatus status)
        {
            // Lógica para actualizar la superficie correcta en la UI
            switch (surface)
            {
                case ToothSurface.Oclusal: tooth.OclusalStatus = status; break;
                case ToothSurface.Mesial: tooth.MesialStatus = status; break;
                case ToothSurface.Distal: tooth.DistalStatus = status; break;
                case ToothSurface.Vestibular: tooth.VestibularStatus = status; break;
                case ToothSurface.Lingual: tooth.LingualStatus = status; break;
                case ToothSurface.Completo: tooth.FullStatus = status; break;
            }
        }

        // [RelayCommand]
        // private void PrintOdontogram()
        // {
        //     // _pdfService.PrintOdontogram(Odontogram);
        // }
    }
}