using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.UI.Messages;
using TuClinica.UI.Views;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;

namespace TuClinica.UI.ViewModels
{
    public partial class PatientDisplayModel : ObservableObject
    {
        [ObservableProperty]
        private string _fullName = "Cargando...";
    }

    public partial class OdontogramViewModel : BaseViewModel, IRecipient<SurfaceClickedMessage>
    {
        private readonly IDialogService _dialogService;
        private readonly IPdfService _pdfService;
        private readonly IFileDialogService _fileDialogService;

        private Patient? _currentPatient;

        [ObservableProperty]
        private PatientDisplayModel _patient = new();

        public ObservableCollection<ToothViewModel> Odontogram { get; } = new();
        public ObservableCollection<ToothViewModel> TeethQuadrant1 { get; } = new();
        public ObservableCollection<ToothViewModel> TeethQuadrant2 { get; } = new();
        public ObservableCollection<ToothViewModel> TeethQuadrant3 { get; } = new();
        public ObservableCollection<ToothViewModel> TeethQuadrant4 { get; } = new();

        [ObservableProperty]
        private bool? _dialogResult;

        public OdontogramViewModel(
            IDialogService dialogService,
            IPdfService pdfService,
            IFileDialogService fileDialogService)
        {
            _dialogService = dialogService;
            _pdfService = pdfService;
            _fileDialogService = fileDialogService;

            WeakReferenceMessenger.Default.Register<SurfaceClickedMessage>(this);
        }

        public void LoadState(ObservableCollection<ToothViewModel> masterOdontogram, Patient? currentPatient)
        {
            _currentPatient = currentPatient;
            Patient.FullName = currentPatient?.PatientDisplayInfo ?? "Paciente Desconocido";

            Odontogram.Clear();
            TeethQuadrant1.Clear();
            TeethQuadrant2.Clear();
            TeethQuadrant3.Clear();
            TeethQuadrant4.Clear();

            // --- INICIO DE LA CORRECCIÓN DE ORDENACIÓN ---

            // Creamos copias locales primero para poder ordenarlas
            var copies = new List<ToothViewModel>();
            foreach (var tooth in masterOdontogram)
            {
                var copy = new ToothViewModel(tooth.ToothNumber)
                {
                    FullCondition = tooth.FullCondition,
                    OclusalCondition = tooth.OclusalCondition,
                    MesialCondition = tooth.MesialCondition,
                    DistalCondition = tooth.DistalCondition,
                    VestibularCondition = tooth.VestibularCondition,
                    LingualCondition = tooth.LingualCondition,
                    FullRestoration = tooth.FullRestoration,
                    OclusalRestoration = tooth.OclusalRestoration,
                    MesialRestoration = tooth.MesialRestoration,
                    DistalRestoration = tooth.DistalRestoration,
                    VestibularRestoration = tooth.VestibularRestoration,
                    LingualRestoration = tooth.LingualRestoration
                };
                Odontogram.Add(copy); // El odontograma maestro (para guardar) mantiene el orden original
                copies.Add(copy); // La lista de copias se usará para la UI
            }

            // Ahora llenamos los cuadrantes para la UI con el orden visual correcto

            // Cuadrante 1 (18 -> 11)
            foreach (var tooth in copies.Where(t => t.ToothNumber >= 11 && t.ToothNumber <= 18).OrderByDescending(t => t.ToothNumber))
            {
                TeethQuadrant1.Add(tooth);
            }

            // Cuadrante 2 (21 -> 28)
            foreach (var tooth in copies.Where(t => t.ToothNumber >= 21 && t.ToothNumber <= 28).OrderBy(t => t.ToothNumber))
            {
                TeethQuadrant2.Add(tooth);
            }

            // Cuadrante 4 (48 -> 41)
            foreach (var tooth in copies.Where(t => t.ToothNumber >= 41 && t.ToothNumber <= 48).OrderByDescending(t => t.ToothNumber))
            {
                TeethQuadrant4.Add(tooth);
            }

            // Cuadrante 3 (31 -> 38)
            foreach (var tooth in copies.Where(t => t.ToothNumber >= 31 && t.ToothNumber <= 38).OrderBy(t => t.ToothNumber))
            {
                TeethQuadrant3.Add(tooth);
            }

            // --- FIN DE LA CORRECCIÓN DE ORDENACIÓN ---
        }

        public void Receive(SurfaceClickedMessage message)
        {
            var tooth = Odontogram.FirstOrDefault(t => t.ToothNumber == message.ToothNumber);
            if (tooth == null) return;
            OpenStateDialog(tooth, message.Value);
        }

        // **** LÓGICA DE ESTADO REESCRITA ****
        private void OpenStateDialog(ToothViewModel tooth, ToothSurface surface)
        {
            var dialog = new OdontogramStateDialog();

            (ToothCondition currentCond, ToothRestoration currentRest) = GetSurfaceState(tooth, surface);

            // Si el diente tiene un estado COMPLETO (Ausente o Implante), mostramos ese.
            if (tooth.FullCondition != ToothCondition.Sano)
            {
                currentCond = tooth.FullCondition;
            }
            if (tooth.FullRestoration != ToothRestoration.Ninguna)
            {
                currentRest = tooth.FullRestoration;
            }

            dialog.LoadState(tooth.ToothNumber, surface, currentCond, currentRest);

            Window? owner = Application.Current.Windows.OfType<OdontogramWindow>().FirstOrDefault();
            if (owner != null)
            {
                dialog.Owner = owner;
            }

            if (dialog.ShowDialog() == true)
            {
                var newCond = dialog.NewCondition;
                var newRest = dialog.NewRestoration;

                // Regla 1: "Ausente" tiene prioridad máxima y limpia todo lo demás.
                if (newCond == ToothCondition.Ausente)
                {
                    UpdateToothSurfaceCondition(tooth, ToothSurface.Completo, ToothCondition.Ausente);
                    UpdateToothSurfaceRestoration(tooth, ToothSurface.Completo, ToothRestoration.Ninguna);
                }
                // Regla 2: Restauraciones de diente completo (Implante, Corona, etc.)
                // (ProtesisFija y Removible también son estados completos)
                else if (newRest == ToothRestoration.Implante ||
                         newRest == ToothRestoration.Corona ||
                         newRest == ToothRestoration.ProtesisFija ||
                         newRest == ToothRestoration.ProtesisRemovible)
                {
                    // Limpia la condición de "Ausente" (si la tuviera)
                    UpdateToothSurfaceCondition(tooth, ToothSurface.Completo, ToothCondition.Sano);
                    // Aplica la restauración completa
                    UpdateToothSurfaceRestoration(tooth, ToothSurface.Completo, newRest);
                }
                // Regla 3: Si se pone "Sano" y "Ninguna" (para limpiar un estado completo)
                else if (newCond == ToothCondition.Sano && newRest == ToothRestoration.Ninguna)
                {
                    UpdateToothSurfaceCondition(tooth, ToothSurface.Completo, ToothCondition.Sano);
                    UpdateToothSurfaceRestoration(tooth, ToothSurface.Completo, ToothRestoration.Ninguna);
                }
                // Regla 4: Es una restauración/condición de superficie (caries, empaste)
                else
                {
                    // Limpia cualquier estado completo (Ausente o Implante)
                    UpdateToothSurfaceCondition(tooth, ToothSurface.Completo, ToothCondition.Sano);

                    // --- ¡AQUÍ ESTÁ EL ERROR DE TIPEO! ---
                    // Cambiado 'Ningna' a 'Ninguna'
                    UpdateToothSurfaceRestoration(tooth, ToothSurface.Completo, ToothRestoration.Ninguna);
                    // --- FIN DE LA CORRECCIÓN ---

                    // Y aplica a la superficie que se hizo clic
                    UpdateToothSurfaceCondition(tooth, surface, newCond);
                    UpdateToothSurfaceRestoration(tooth, surface, newRest);
                }
            }
        }

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

        private void UpdateToothSurfaceRestoration(ToothViewModel tooth, ToothSurface surface, ToothRestoration restoration)
        {
            if (surface == ToothSurface.Completo)
            {
                tooth.OclusalRestoration = ToothRestoration.Ninguna;
                tooth.MesialRestoration = ToothRestoration.Ninguna;
                tooth.DistalRestoration = ToothRestoration.Ninguna;
                tooth.VestibularRestoration = ToothRestoration.Ninguna;
                tooth.LingualRestoration = ToothRestoration.Ninguna;
                tooth.FullRestoration = restoration;
                return;
            }

            tooth.FullRestoration = ToothRestoration.Ninguna;
            switch (surface)
            {
                case ToothSurface.Oclusal: tooth.OclusalRestoration = restoration; break;
                case ToothSurface.Mesial: tooth.MesialRestoration = restoration; break;
                case ToothSurface.Distal: tooth.DistalRestoration = restoration; break;
                case ToothSurface.Vestibular: tooth.VestibularRestoration = restoration; break;
                case ToothSurface.Lingual: tooth.LingualRestoration = restoration; break;
            }
        }

        private void UpdateToothSurfaceCondition(ToothViewModel tooth, ToothSurface surface, ToothCondition condition)
        {
            if (surface == ToothSurface.Completo)
            {
                tooth.OclusalCondition = ToothCondition.Sano;
                tooth.MesialCondition = ToothCondition.Sano;
                tooth.DistalCondition = ToothCondition.Sano;
                tooth.VestibularCondition = ToothCondition.Sano;
                tooth.LingualCondition = ToothCondition.Sano;
                tooth.FullCondition = condition;
                return;
            }

            tooth.FullCondition = ToothCondition.Sano;
            switch (surface)
            {
                case ToothSurface.Oclusal: tooth.OclusalCondition = condition; break;
                case ToothSurface.Mesial: tooth.MesialCondition = condition; break;
                case ToothSurface.Distal: tooth.DistalCondition = condition; break;
                case ToothSurface.Vestibular: tooth.VestibularCondition = condition; break;
                case ToothSurface.Lingual: tooth.LingualCondition = condition; break;
            }
        }

        [RelayCommand]
        private void Accept()
        {
            DialogResult = true;
        }

        [RelayCommand]
        private void Cancel()
        {
            DialogResult = false;
        }

        [RelayCommand]
        private async Task Print()
        {
            if (_currentPatient == null)
            {
                _dialogService.ShowMessage("No hay ningún paciente cargado para generar el PDF.", "Error");
                return;
            }

            try
            {
                string jsonState = GetSerializedState();
                string generatedFilePath = await _pdfService.GenerateOdontogramPdfAsync(_currentPatient, jsonState);

                var result = _dialogService.ShowConfirmation(
                    $"PDF del odontograma generado con éxito en:\n{generatedFilePath}\n\n¿Desea abrir el archivo ahora?",
                    "Éxito");

                if (result == CoreDialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(generatedFilePath) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al generar el PDF del odontograma:\n{ex.Message}", "Error de Impresión");
            }
        }

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