using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.UI.Messages;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;

namespace TuClinica.UI.ViewModels
{
    // Enum para identificar qué herramienta tiene el doctor en la mano
    public enum OdontogramTool
    {
        Cursor,         // Solo ver/seleccionar
        Borrador,       // Limpiar estado (Sano/Ninguna)
        Caries,         // Marcar condición
        Fractura,       // Marcar condición
        Obturacion,     // Marcar restauración
        Corona,         // Restauración completa
        Endodoncia,     // Restauración completa
        Implante,       // Restauración completa
        Ausente,        // Condición especial
        Extraccion      // Condición especial
    }

    public class ToothStateSummary
    {
        public int ToothNumber { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Brush ColorIndicator { get; set; } = Brushes.Transparent;
    }

    public partial class PatientDisplayModel : ObservableObject
    {
        [ObservableProperty]
        private string _fullName = "Cargando...";
    }

    public partial class OdontogramViewModel : BaseViewModel, IRecipient<SurfaceClickedMessage>
    {
        private readonly IDialogService _dialogService;
        private readonly IPdfService _pdfService;

        private Patient? _currentPatient;

        [ObservableProperty]
        private PatientDisplayModel _patient = new();

        public ObservableCollection<ToothViewModel> Odontogram { get; } = new();

        [ObservableProperty]
        private ObservableCollection<ToothStateSummary> _summaryList = new();

        // --- NUEVO: Estado de la Herramienta Activa ---
        [ObservableProperty]
        private OdontogramTool _selectedTool = OdontogramTool.Cursor;

        [ObservableProperty]
        private bool? _dialogResult;

        public OdontogramViewModel(IDialogService dialogService, IPdfService pdfService)
        {
            _dialogService = dialogService;
            _pdfService = pdfService;

            // Registrarse para recibir el clic desde el diente
            WeakReferenceMessenger.Default.Register<SurfaceClickedMessage>(this);
        }

        public void LoadState(ObservableCollection<ToothViewModel> masterOdontogram, Patient? currentPatient)
        {
            _currentPatient = currentPatient;
            Patient.FullName = currentPatient?.PatientDisplayInfo ?? "Paciente Desconocido";
            Odontogram.Clear();

            foreach (var tooth in masterOdontogram)
            {
                // Clonamos el estado para no afectar al maestro hasta que se guarde
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
            UpdateSummary();
        }

        // --- LÓGICA PRINCIPAL: Reacción al Clic ---
        public void Receive(SurfaceClickedMessage message)
        {
            // Si estamos en modo cursor, no hacemos nada (o podríamos seleccionar el diente para ver detalles)
            if (SelectedTool == OdontogramTool.Cursor) return;

            var tooth = Odontogram.FirstOrDefault(t => t.ToothNumber == message.ToothNumber);
            if (tooth == null) return;

            // Aplicar la herramienta activa directamente
            ApplyToolToTooth(tooth, message.Value);
        }

        private void ApplyToolToTooth(ToothViewModel tooth, ToothSurface surface)
        {
            // 1. Lógica para Herramientas Globales (Afectan a todo el diente)
            if (IsGlobalTool(SelectedTool))
            {
                // Limpiar estados previos conflictivos
                ResetToothToHealthy(tooth);

                switch (SelectedTool)
                {
                    case OdontogramTool.Ausente:
                        tooth.FullCondition = ToothCondition.Ausente;
                        ApplyConditionToAllSurfaces(tooth, ToothCondition.Ausente);
                        break;
                    case OdontogramTool.Extraccion:
                        tooth.FullCondition = ToothCondition.ExtraccionIndicada;
                        break;
                    case OdontogramTool.Corona:
                        tooth.FullRestoration = ToothRestoration.Corona;
                        ApplyRestorationToAllSurfaces(tooth, ToothRestoration.Corona);
                        break;
                    case OdontogramTool.Implante:
                        tooth.FullRestoration = ToothRestoration.Implante;
                        ApplyRestorationToAllSurfaces(tooth, ToothRestoration.Implante);
                        break;
                    case OdontogramTool.Endodoncia:
                        tooth.FullRestoration = ToothRestoration.Endodoncia;
                        // Endodoncia no cubre visualmente las caras externas, es interna
                        break;
                    case OdontogramTool.Borrador:
                        // Ya se reseteó arriba
                        break;
                }
            }
            // 2. Lógica para Herramientas de Superficie (Caries, Obturación...)
            else
            {
                // Si el diente estaba ausente o con implante, resetearlo para permitir edición de caras
                if (tooth.FullCondition == ToothCondition.Ausente || IsFullRestoration(tooth.FullRestoration))
                {
                    ResetToothToHealthy(tooth);
                }

                // Determinar qué estamos pintando
                switch (SelectedTool)
                {
                    case OdontogramTool.Caries:
                        UpdateSurfaceCondition(tooth, surface, ToothCondition.Caries);
                        break;
                    case OdontogramTool.Fractura:
                        UpdateSurfaceCondition(tooth, surface, ToothCondition.Fractura);
                        break;
                    case OdontogramTool.Obturacion:
                        UpdateSurfaceRestoration(tooth, surface, ToothRestoration.Obturacion);
                        break;
                }
            }

            UpdateSummary();
        }

        // --- Comandos para la Toolbar ---
        [RelayCommand]
        private void SelectTool(string toolName)
        {
            if (Enum.TryParse<OdontogramTool>(toolName, true, out var tool))
            {
                SelectedTool = tool;
            }
        }

        // --- Helpers Lógicos ---
        private bool IsGlobalTool(OdontogramTool tool)
        {
            return tool == OdontogramTool.Ausente ||
                   tool == OdontogramTool.Extraccion ||
                   tool == OdontogramTool.Corona ||
                   tool == OdontogramTool.Implante ||
                   tool == OdontogramTool.Endodoncia ||
                   tool == OdontogramTool.Borrador;
        }

        private void UpdateSurfaceCondition(ToothViewModel tooth, ToothSurface surface, ToothCondition condition)
        {
            // Si pintamos caries, quitamos restauración en esa cara
            UpdateSurfaceRestoration(tooth, surface, ToothRestoration.Ninguna);

            switch (surface)
            {
                case ToothSurface.Oclusal: tooth.OclusalCondition = condition; break;
                case ToothSurface.Mesial: tooth.MesialCondition = condition; break;
                case ToothSurface.Distal: tooth.DistalCondition = condition; break;
                case ToothSurface.Vestibular: tooth.VestibularCondition = condition; break;
                case ToothSurface.Lingual: tooth.LingualCondition = condition; break;
                case ToothSurface.Completo:
                    tooth.FullCondition = condition;
                    ApplyConditionToAllSurfaces(tooth, condition);
                    break;
            }
        }

        private void UpdateSurfaceRestoration(ToothViewModel tooth, ToothSurface surface, ToothRestoration restoration)
        {
            // Si pintamos obturación, quitamos caries en esa cara (asumimos curado)
            // OJO: Esto depende de tu lógica. A veces quieres marcar "Caries sobre obturación".
            // Para simplificar el modo "pintar", el último gana.
            if (restoration != ToothRestoration.Ninguna)
                UpdateSurfaceCondition(tooth, surface, ToothCondition.Sano);

            switch (surface)
            {
                case ToothSurface.Oclusal: tooth.OclusalRestoration = restoration; break;
                case ToothSurface.Mesial: tooth.MesialRestoration = restoration; break;
                case ToothSurface.Distal: tooth.DistalRestoration = restoration; break;
                case ToothSurface.Vestibular: tooth.VestibularRestoration = restoration; break;
                case ToothSurface.Lingual: tooth.LingualRestoration = restoration; break;
                case ToothSurface.Completo:
                    tooth.FullRestoration = restoration;
                    ApplyRestorationToAllSurfaces(tooth, restoration);
                    break;
            }
        }

        private void ResetToothToHealthy(ToothViewModel tooth)
        {
            tooth.FullCondition = ToothCondition.Sano;
            tooth.FullRestoration = ToothRestoration.Ninguna;
            ApplyConditionToAllSurfaces(tooth, ToothCondition.Sano);
            ApplyRestorationToAllSurfaces(tooth, ToothRestoration.Ninguna);
        }

        private void ApplyConditionToAllSurfaces(ToothViewModel tooth, ToothCondition condition)
        {
            tooth.OclusalCondition = condition;
            tooth.MesialCondition = condition;
            tooth.DistalCondition = condition;
            tooth.VestibularCondition = condition;
            tooth.LingualCondition = condition;
        }

        private void ApplyRestorationToAllSurfaces(ToothViewModel tooth, ToothRestoration restoration)
        {
            tooth.OclusalRestoration = restoration;
            tooth.MesialRestoration = restoration;
            tooth.DistalRestoration = restoration;
            tooth.VestibularRestoration = restoration;
            tooth.LingualRestoration = restoration;
        }

        private bool IsFullRestoration(ToothRestoration r)
        {
            return r == ToothRestoration.Corona || r == ToothRestoration.Implante || r == ToothRestoration.Endodoncia;
        }

        private void UpdateSummary()
        {
            SummaryList.Clear();
            var teeth = Odontogram.OrderBy(t => t.ToothNumber);

            foreach (var t in teeth)
            {
                if (t.FullCondition == ToothCondition.Ausente)
                {
                    AddSummary(t.ToothNumber, "Ausente", "Estado", Brushes.Gray);
                    continue;
                }
                if (t.FullCondition == ToothCondition.ExtraccionIndicada)
                    AddSummary(t.ToothNumber, "Extracción Indicada", "Condición", Brushes.Red);

                if (t.FullRestoration == ToothRestoration.Endodoncia)
                    AddSummary(t.ToothNumber, "Endodoncia", "Tratamiento", Brushes.Purple);

                if (t.FullRestoration == ToothRestoration.Implante)
                {
                    AddSummary(t.ToothNumber, "Implante", "Tratamiento", Brushes.SlateGray);
                    continue;
                }
                if (t.FullRestoration == ToothRestoration.Corona)
                {
                    AddSummary(t.ToothNumber, "Corona", "Tratamiento", Brushes.Gold);
                    continue;
                }

                // Chequear Caras Individuales
                CheckFace(t.ToothNumber, "Oclusal", t.OclusalCondition, t.OclusalRestoration);
                CheckFace(t.ToothNumber, "Mesial", t.MesialCondition, t.MesialRestoration);
                CheckFace(t.ToothNumber, "Distal", t.DistalCondition, t.DistalRestoration);
                CheckFace(t.ToothNumber, "Vestibular", t.VestibularCondition, t.VestibularRestoration);
                CheckFace(t.ToothNumber, "Lingual", t.LingualCondition, t.LingualRestoration);
            }
        }

        private void CheckFace(int num, string face, ToothCondition cond, ToothRestoration rest)
        {
            if (cond == ToothCondition.Caries)
                AddSummary(num, $"Caries {face}", "Patología", Brushes.Red);
            else if (cond == ToothCondition.Fractura)
                AddSummary(num, $"Fractura {face}", "Patología", Brushes.Orange);

            if (rest == ToothRestoration.Obturacion)
                AddSummary(num, $"Obturación {face}", "Tratamiento", Brushes.RoyalBlue);
        }

        private void AddSummary(int tooth, string desc, string type, Brush color)
        {
            SummaryList.Add(new ToothStateSummary { ToothNumber = tooth, Description = desc, Type = type, ColorIndicator = color });
        }

        [RelayCommand] private void Accept() => DialogResult = true;
        [RelayCommand] private void Cancel() => DialogResult = false;

        [RelayCommand]
        private async Task Print()
        {
            if (_currentPatient == null) return;
            try
            {
                string jsonState = GetSerializedState();
                string path = await _pdfService.GenerateOdontogramPdfAsync(_currentPatient, jsonState);
                if (_dialogService.ShowConfirmation($"PDF Generado. ¿Abrir?", "Éxito") == CoreDialogResult.Yes)
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
        }

        public string GetSerializedState()
        {
            try { return JsonSerializer.Serialize(Odontogram); } catch { return string.Empty; }
        }
    }
}