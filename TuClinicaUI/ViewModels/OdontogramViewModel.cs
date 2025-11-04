using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Linq;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models; // Necesario para Patient
using TuClinica.UI.Messages;
using TuClinica.UI.Views;

namespace TuClinica.UI.ViewModels
{
    // --- CLASE AUXILIAR DE DISPLAY (Debe estar fuera de la principal) ---
    public partial class PatientDisplayModel : ObservableObject
    {
        [ObservableProperty]
        private string _fullName = "Cargando...";
    }
    // --------------------------------------------------------------------

    /// <summary>
    /// ViewModel para la VENTANA MODAL del odontograma interactivo.
    /// </summary>
    public partial class OdontogramViewModel : BaseViewModel, IRecipient<SurfaceClickedMessage>
    {


        [ObservableProperty]
        private ObservableCollection<Treatment> _availableTreatments = new();
        private readonly IDialogService _dialogService;
        // private readonly IPdfService _pdfService; // Descomentar si implementas impresión

        [ObservableProperty]
        private PatientDisplayModel _patient = new(); // Se enlaza con el título de la ventana

        // Colección maestra (usada internamente)
        public ObservableCollection<ToothViewModel> Odontogram { get; } = new();

        // Colecciones para el Data Binding en el XAML (los 4 cuadrantes)
        public ObservableCollection<ToothViewModel> TeethQuadrant1 { get; } = new();
        public ObservableCollection<ToothViewModel> TeethQuadrant2 { get; } = new();
        public ObservableCollection<ToothViewModel> TeethQuadrant3 { get; } = new();
        public ObservableCollection<ToothViewModel> TeethQuadrant4 { get; } = new();


        public OdontogramViewModel(IDialogService dialogService/*, IPdfService pdfService*/)
        {
            _dialogService = dialogService;
            // _pdfService = pdfService;

            // Escuchar clics desde los ToothViewModels
            WeakReferenceMessenger.Default.Register<SurfaceClickedMessage>(this);
        }
       
        /// <summary>
        /// Carga el estado actual del odontograma maestro (desde PatientFileViewModel)
        /// y la información del paciente para el título, y distribuye los dientes en 4 cuadrantes.
        /// </summary>
        public void LoadState(ObservableCollection<ToothViewModel> masterOdontogram, Patient? currentPatient)
        {
            // 1. Cargar el nombre del paciente para el título
            if (currentPatient != null)
            {
                // Usamos la propiedad PatientDisplayInfo de Core.Models.Patient
                Patient.FullName = currentPatient.PatientDisplayInfo;
            }
            else
            {
                Patient.FullName = "Paciente Desconocido";
            }

            // 2. Cargar el estado de los dientes y distribuirlos
            Odontogram.Clear();
            TeethQuadrant1.Clear();
            TeethQuadrant2.Clear();
            TeethQuadrant3.Clear();
            TeethQuadrant4.Clear();

            foreach (var tooth in masterOdontogram)
            {
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

                // Distribución por cuadrante (Notación FDI: primer dígito es el cuadrante)
                if (copy.ToothNumber >= 11 && copy.ToothNumber <= 18)
                {
                    TeethQuadrant1.Add(copy);
                }
                else if (copy.ToothNumber >= 21 && copy.ToothNumber <= 28)
                {
                    TeethQuadrant2.Add(copy);
                }
                else if (copy.ToothNumber >= 31 && copy.ToothNumber <= 38)
                {
                    TeethQuadrant3.Add(copy);
                }
                else if (copy.ToothNumber >= 41 && copy.ToothNumber <= 48)
                {
                    TeethQuadrant4.Add(copy);
                }

                // NOTA: El orden dentro de cada cuadrante está definido por la inicialización en PatientFileViewModel.cs
                // (e.g., C1: 18->11, C2: 21->28, C4: 41->48, C3: 38->31)
            }
        }

        /// <summary>
        /// Recibe el clic de un ToothViewModel y gestiona la apertura del diálogo y la actualización de la UI.
        /// </summary>
        public void Receive(SurfaceClickedMessage message)
        {
            var tooth = Odontogram.FirstOrDefault(t => t.ToothNumber == message.ToothNumber);
            if (tooth == null) return;

            // ABRIR EL DIÁLOGO CON TRATAMIENTOS
            OpenTreatmentDialog(tooth, message.Value);
        }

        private void OpenTreatmentDialog(ToothViewModel tooth, ToothSurface surface)
        {
            var dialog = new TreatmentPriceDialog();

            // CLAVE: PASAR LOS TRATAMIENTOS
            dialog.AvailableTreatments = AvailableTreatments;

            if (dialog.ShowDialog() == true)
            {
                WeakReferenceMessenger.Default.Send(new RegisterTreatmentMessage(
                    tooth.ToothNumber,
                    surface,
                    dialog.SelectedTreatmentId ?? 0,
                    dialog.SelectedRestoration ?? ToothRestoration.Ninguna,
                    dialog.Price
                ));

                // ACTUALIZAR UI LOCAL
                UpdateToothSurfaceRestoration(tooth, surface, dialog.SelectedRestoration ?? ToothRestoration.Ninguna);
                UpdateToothSurfaceCondition(tooth, surface, ToothCondition.Sano);
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

        [RelayCommand]
        private void Save()
        {
            // Lógica de guardado (implementación pendiente en su proyecto)
            // Esto debería enviar el odontograma maestro de vuelta a PatientFileViewModel para persistencia.
        }

        [RelayCommand]
        private void ApplyTreatment()
        {
            // Lógica para aplicar un tratamiento general o abrir una ventana de selección (implementación pendiente)
        }

        [RelayCommand]
        private void Close()
        {
            // Lógica para cerrar la ventana (se maneja en el código detrás o con un mensaje)
        }
    }
}