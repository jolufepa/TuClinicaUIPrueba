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
using System.Collections.Generic;

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

        // Colección única para TODOS los dientes (el SVG los posiciona)
        public ObservableCollection<ToothViewModel> Odontogram { get; } = new();

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

            // Copiamos los dientes del maestro a la colección local para la edición
            // No hace falta ordenar ni separar en cuadrantes, las coordenadas absolutas del SVG 
            // se encargan de colocar cada diente en su sitio visualmente.
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
                Odontogram.Add(copy);
            }
        }

        public void Receive(SurfaceClickedMessage message)
        {
            var tooth = Odontogram.FirstOrDefault(t => t.ToothNumber == message.ToothNumber);
            if (tooth == null) return;
            OpenStateDialog(tooth, message.Value);
        }

        private void OpenStateDialog(ToothViewModel tooth, ToothSurface surface)
        {
            var dialog = new OdontogramStateDialog();

            (ToothCondition currentCond, ToothRestoration currentRest) = GetSurfaceState(tooth, surface);

            // Si el diente tiene una condición global (ej. Ausente), esa prevalece para mostrar en el diálogo
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

                // Lógica para aplicar cambios
                if (newCond == ToothCondition.Ausente)
                {
                    // Si se marca ausente, afecta a todo el diente
                    UpdateToothSurfaceCondition(tooth, ToothSurface.Completo, ToothCondition.Ausente);
                    UpdateToothSurfaceRestoration(tooth, ToothSurface.Completo, ToothRestoration.Ninguna);
                }
                else if (newRest == ToothRestoration.Implante ||
                         newRest == ToothRestoration.Corona ||
                         newRest == ToothRestoration.ProtesisFija ||
                         newRest == ToothRestoration.ProtesisRemovible)
                {
                    // Restauraciones completas
                    UpdateToothSurfaceCondition(tooth, ToothSurface.Completo, ToothCondition.Sano); // Limpiar condiciones previas
                    UpdateToothSurfaceRestoration(tooth, ToothSurface.Completo, newRest);
                }
                else if (newCond == ToothCondition.Sano && newRest == ToothRestoration.Ninguna)
                {
                    // Si se "limpia" la superficie, y estaba marcado como completo antes, reseteamos el completo
                    if (surface == ToothSurface.Completo || tooth.FullCondition != ToothCondition.Sano || tooth.FullRestoration != ToothRestoration.Ninguna)
                    {
                        UpdateToothSurfaceCondition(tooth, ToothSurface.Completo, ToothCondition.Sano);
                        UpdateToothSurfaceRestoration(tooth, ToothSurface.Completo, ToothRestoration.Ninguna);
                    }

                    // Y limpiamos la superficie específica
                    UpdateToothSurfaceCondition(tooth, surface, ToothCondition.Sano);
                    UpdateToothSurfaceRestoration(tooth, surface, ToothRestoration.Ninguna);
                }
                else
                {
                    // Cambio puntual en una superficie
                    // Primero aseguramos que no esté marcado como "Completo" (ej. Ausente) para que se vea la superficie
                    if (tooth.FullCondition == ToothCondition.Ausente)
                        UpdateToothSurfaceCondition(tooth, ToothSurface.Completo, ToothCondition.Sano);

                    if (IsFullRestoration(tooth.FullRestoration))
                        UpdateToothSurfaceRestoration(tooth, ToothSurface.Completo, ToothRestoration.Ninguna);

                    UpdateToothSurfaceCondition(tooth, surface, newCond);
                    UpdateToothSurfaceRestoration(tooth, surface, newRest);
                }
            }
        }

        private bool IsFullRestoration(ToothRestoration r)
        {
            return r == ToothRestoration.Corona || r == ToothRestoration.Implante || r == ToothRestoration.ProtesisFija || r == ToothRestoration.ProtesisRemovible;
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
                // Nota: El método GenerateOdontogramPdfAsync en PdfService necesitará ser actualizado 
                // para usar las nuevas geometrías si quieres que el PDF también sea anatómico.
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