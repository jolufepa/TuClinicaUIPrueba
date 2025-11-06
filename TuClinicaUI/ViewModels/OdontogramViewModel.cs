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
// --- USINGS AÑADIDOS PARA IMPRESIÓN PDF ---
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows; // Para Window
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult; // Alias
// -------------------------------------

namespace TuClinica.UI.ViewModels
{
    // --- CLASE AUXILIAR DE DISPLAY (Debe estar fuera de la principal) ---
    public partial class PatientDisplayModel : ObservableObject
    {
        [ObservableProperty]
        private string _fullName = "Cargando...";
    }
    // --------------------------------------------------------------------

    public partial class OdontogramViewModel : BaseViewModel, IRecipient<SurfaceClickedMessage>
    {
        private readonly IDialogService _dialogService;
        // --- SERVICIOS AÑADIDOS ---
        private readonly IPdfService _pdfService;
        private readonly IFileDialogService _fileDialogService; // Sigue siendo necesario para otros diálogos

        // --- CAMPO AÑADIDO PARA GUARDAR EL PACIENTE ---
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
            _fileDialogService = fileDialogService; // Lo mantenemos por si se usa en otro lado

            WeakReferenceMessenger.Default.Register<SurfaceClickedMessage>(this);
        }

        public void LoadState(ObservableCollection<ToothViewModel> masterOdontogram, Patient? currentPatient)
        {
            _currentPatient = currentPatient;

            if (currentPatient != null)
            {
                Patient.FullName = currentPatient.PatientDisplayInfo;
            }
            else
            {
                Patient.FullName = "Paciente Desconocido";
            }

            Odontogram.Clear();
            TeethQuadrant1.Clear();
            TeethQuadrant2.Clear();
            TeethQuadrant3.Clear();
            TeethQuadrant4.Clear();

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
                if (copy.ToothNumber >= 11 && copy.ToothNumber <= 18) TeethQuadrant1.Add(copy);
                else if (copy.ToothNumber >= 21 && copy.ToothNumber <= 28) TeethQuadrant2.Add(copy);
                else if (copy.ToothNumber >= 31 && copy.ToothNumber <= 38) TeethQuadrant3.Add(copy);
                else if (copy.ToothNumber >= 41 && copy.ToothNumber <= 48) TeethQuadrant4.Add(copy);
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
            dialog.LoadState(tooth.ToothNumber, surface, currentCond, currentRest);
            if (dialog.ShowDialog() == true)
            {
                UpdateToothSurfaceCondition(tooth, surface, dialog.NewCondition);
                UpdateToothSurfaceRestoration(tooth, surface, dialog.NewRestoration);
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

        // *** COMANDO DE IMPRESIÓN CORREGIDO ***
        [RelayCommand]
        private async Task Print()
        {
            if (_currentPatient == null)
            {
                _dialogService.ShowMessage("No hay ningún paciente cargado para generar el PDF.", "Error");
                return;
            }

            // *** CORRECCIÓN: Eliminada la llamada a _fileDialogService.ShowSaveDialog ***
            // Ya no preguntamos al usuario dónde guardar.

            try
            {
                // 2. Obtener el estado JSON actual
                string jsonState = GetSerializedState();

                // 3. Llamar al servicio de PDF.
                // Este método ahora crea el nombre de archivo único automáticamente.
                string generatedFilePath = await _pdfService.GenerateOdontogramPdfAsync(_currentPatient, jsonState);

                // 4. Confirmar y abrir
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