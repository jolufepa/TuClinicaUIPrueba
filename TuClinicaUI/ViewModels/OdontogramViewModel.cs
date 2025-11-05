using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json; // *** AÑADIDO PARA JSON ***
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models; // Necesario para Patient
using TuClinica.UI.Messages;
using TuClinica.UI.Views; // *** AÑADIDO PARA EL NUEVO DIÁLOGO ***

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
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private PatientDisplayModel _patient = new(); // Se enlaza con el título de la ventana

        // Colección maestra (usada internamente)
        public ObservableCollection<ToothViewModel> Odontogram { get; } = new();

        // Colecciones para el Data Binding en el XAML (los 4 cuadrantes)
        public ObservableCollection<ToothViewModel> TeethQuadrant1 { get; } = new();
        public ObservableCollection<ToothViewModel> TeethQuadrant2 { get; } = new();
        public ObservableCollection<ToothViewModel> TeethQuadrant3 { get; } = new();
        public ObservableCollection<ToothViewModel> TeethQuadrant4 { get; } = new();

        [ObservableProperty]
        private bool? _dialogResult;

        public OdontogramViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;

            // Escuchar clics desde los ToothViewModels
            WeakReferenceMessenger.Default.Register<SurfaceClickedMessage>(this);
        }

        public void LoadState(ObservableCollection<ToothViewModel> masterOdontogram, Patient? currentPatient)
        {
            // 1. Cargar el nombre del paciente para el título
            if (currentPatient != null)
            {
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

                // Distribución por cuadrante
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
            }
        }

        public void Receive(SurfaceClickedMessage message)
        {
            var tooth = Odontogram.FirstOrDefault(t => t.ToothNumber == message.ToothNumber);
            if (tooth == null) return;

            // *** CAMBIO: Llamada al nuevo diálogo ***
            OpenStateDialog(tooth, message.Value);
        }

        // *** CAMBIO: Lógica de diálogo modificada ***
        private void OpenStateDialog(ToothViewModel tooth, ToothSurface surface)
        {
            // 1. Crear el nuevo diálogo visual
            var dialog = new OdontogramStateDialog();

            // 2. Obtener el estado actual de la superficie clicada
            (ToothCondition currentCond, ToothRestoration currentRest) = GetSurfaceState(tooth, surface);

            // 3. Cargar el diálogo con ese estado
            dialog.LoadState(tooth.ToothNumber, surface, currentCond, currentRest);

            // 4. Mostrar el diálogo
            if (dialog.ShowDialog() == true)
            {
                // 5. Si el usuario aceptó, aplicar los nuevos estados
                UpdateToothSurfaceCondition(tooth, surface, dialog.NewCondition);
                UpdateToothSurfaceRestoration(tooth, surface, dialog.NewRestoration);
            }
        }

        // --- FUNCIONES DE ACTUALIZACIÓN DE ESTADO ---

        // *** CAMBIO: Nuevo método auxiliar ***
        /// <summary>
        /// Obtiene la Condición y Restauración actual de una superficie específica.
        /// </summary>
        private (ToothCondition, ToothRestoration) GetSurfaceState(ToothViewModel tooth, ToothSurface surface)
        {
            return surface switch
            {
                ToothSurface.Oclusal => (tooth.OclusalCondition, tooth.OclusalRestoration),
                ToothSurface.Mesial => (tooth.MesialCondition, tooth.MesialRestoration),
                ToothSurface.Distal => (tooth.DistalCondition, tooth.DistalRestoration),
                ToothSurface.Vestibular => (tooth.VestibularCondition, tooth.VestibularRestoration),
                ToothSurface.Lingual => (tooth.LingualCondition, tooth.LingualRestoration),
                ToothSurface.Completo => (tooth.FullCondition, tooth.FullRestoration),
                _ => (ToothCondition.Sano, ToothRestoration.Ninguna)
            };
        }

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
            DialogResult = true;
        }

        [RelayCommand]
        private void Close()
        {
            DialogResult = false;
        }

        /// <summary>
        /// Serializa la colección Odontogram (solo los 32 ToothViewModels) a JSON.
        /// </summary>
        public string GetSerializedState()
        {
            try
            {
                return JsonSerializer.Serialize(Odontogram);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al serializar el estado del odontograma: {ex.Message}", "Error JSON");
                return string.Empty;
            }
        }
    }
}