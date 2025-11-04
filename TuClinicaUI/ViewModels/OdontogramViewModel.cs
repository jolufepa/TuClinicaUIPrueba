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
                    // Propiedades de Condición
                    FullCondition = tooth.FullCondition,
                    OclusalCondition = tooth.OclusalCondition,
                    MesialCondition = tooth.MesialCondition,
                    DistalCondition = tooth.DistalCondition,
                    VestibularCondition = tooth.VestibularCondition,
                    LingualCondition = tooth.LingualCondition,

                    // Propiedades de Restauración
                    FullRestoration = tooth.FullRestoration,
                    OclusalRestoration = tooth.OclusalRestoration,
                    MesialRestoration = tooth.MesialRestoration,
                    DistalRestoration = tooth.DistalRestoration,
                    VestibularRestoration = tooth.VestibularRestoration,
                    LingualRestoration = tooth.LingualRestoration
                };
                Odontogram.Add(copy);
            }
        }

        /// <summary>
        /// Recibe el clic de un ToothViewModel y gestiona la apertura del diálogo y la actualización de la UI.
        /// </summary>
        public void Receive(SurfaceClickedMessage message)
        {
            // RESOLUCIÓN CS0029 (Línea 69): Ahora el método tiene 4 elementos.
            // Usamos 'price' como nullable (decimal?) y 'restoration' como RestorationResult.
            var (ok, treatmentId, restoration, price) = _dialogService.ShowTreatmentPriceDialog();

            // Chequeamos todos los tipos nullable.
            if (ok && treatmentId.HasValue && restoration.HasValue && price.HasValue)
            {
                // 2. Enviar mensaje al PatientFileViewModel
                WeakReferenceMessenger.Default.Send(new RegisterTreatmentMessage(
                    message.ToothNumber,
                    message.Value, // La superficie (ToothSurface)
                    treatmentId.Value, // int? -> int
                    restoration.Value, // ToothRestoration? -> ToothRestoration
                    price.Value // decimal? -> decimal
                ));

                // 3. Actualizar nuestra UI local para feedback instantáneo
                var toothToUpdate = Odontogram.FirstOrDefault(t => t.ToothNumber == message.ToothNumber);
                if (toothToUpdate != null)
                {
                    UpdateToothSurfaceRestoration(toothToUpdate, message.Value, restoration.Value);
                    UpdateToothSurfaceCondition(toothToUpdate, message.Value, ToothCondition.Sano);
                }
            }
        }

        // --- FUNCIONES DE ACTUALIZACIÓN DE ESTADO ---

        /// <summary>
        /// Actualiza la propiedad de RESTAURACIÓN de la superficie (capa superior visual).
        /// </summary>
        private void UpdateToothSurfaceRestoration(ToothViewModel tooth, ToothSurface surface, ToothRestoration restoration)
        {
            switch (surface)
            {
                case ToothSurface.Oclusal: tooth.OclusalRestoration = restoration; break;
                case ToothSurface.Mesial: tooth.MesialRestoration = restoration; break;
                case ToothSurface.Distal: tooth.DistalRestoration = restoration; break;
                case ToothSurface.Vestibular: tooth.VestibularRestoration = restoration; break;
                case ToothSurface.Lingual: tooth.LingualRestoration = restoration; break;
                case ToothSurface.Completo: tooth.FullRestoration = restoration; break;
            }
        }

        /// <summary>
        /// Actualiza la propiedad de CONDICIÓN de la superficie (capa de fondo visual).
        /// </summary>
        private void UpdateToothSurfaceCondition(ToothViewModel tooth, ToothSurface surface, ToothCondition condition)
        {
            switch (surface)
            {
                case ToothSurface.Oclusal: tooth.OclusalCondition = condition; break;
                case ToothSurface.Mesial: tooth.MesialCondition = condition; break;
                case ToothSurface.Distal: tooth.DistalCondition = condition; break;
                case ToothSurface.Vestibular: tooth.VestibularCondition = condition; break;
                case ToothSurface.Lingual: tooth.LingualCondition = condition; break;
                case ToothSurface.Completo: tooth.FullCondition = condition; break;
            }
        }
        // [RelayCommand]
        // private void PrintOdontogram()
        // {
        //    // _pdfService.PrintOdontogram(Odontogram);
        // }
    }
}