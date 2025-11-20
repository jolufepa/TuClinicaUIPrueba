using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.UI.Messages;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;

namespace TuClinica.UI.ViewModels
{
    public enum OdontogramTool
    {
        Cursor, Borrador, Caries, Fractura, Obturacion, Corona, Endodoncia, Implante, Ausente, Extraccion,
        Puente, Ortodoncia
    }

    public class ToothStateSummary
    {
        public int ToothNumber { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Brush ColorIndicator { get; set; } = Brushes.Transparent;
    }

    // --- DTO LIMPIO PARA SERIALIZACIÓN ---
    // Usamos este objeto simple para asegurar que 'ToothNumber' y los estados se guarden correctamente
    public class ToothStateDto
    {
        public int ToothNumber { get; set; }
        public ToothCondition FullCondition { get; set; }
        public ToothCondition OclusalCondition { get; set; }
        public ToothCondition MesialCondition { get; set; }
        public ToothCondition DistalCondition { get; set; }
        public ToothCondition VestibularCondition { get; set; }
        public ToothCondition LingualCondition { get; set; }

        public ToothRestoration FullRestoration { get; set; }
        public ToothRestoration OclusalRestoration { get; set; }
        public ToothRestoration MesialRestoration { get; set; }
        public ToothRestoration DistalRestoration { get; set; }
        public ToothRestoration VestibularRestoration { get; set; }
        public ToothRestoration LingualRestoration { get; set; }
    }

    public class OdontogramPersistenceWrapper
    {
        // Cambiamos la lista para usar el DTO en lugar del ViewModel
        public List<ToothStateDto> Teeth { get; set; } = new();
        public List<DentalConnector> Connectors { get; set; } = new();
    }

    public partial class PatientDisplayModel : ObservableObject
    {
        [ObservableProperty] private string _fullName = "Cargando...";
    }

    public partial class OdontogramViewModel : BaseViewModel, IRecipient<SurfaceClickedMessage>
    {
        private readonly IDialogService _dialogService;
        private readonly IPdfService _pdfService;
        private Patient? _currentPatient;

        [ObservableProperty] private PatientDisplayModel _patient = new();
        public ObservableCollection<ToothViewModel> Odontogram { get; } = new();
        public ObservableCollection<DentalConnector> Connectors { get; } = new();
        [ObservableProperty] private ObservableCollection<ToothStateSummary> _summaryList = new();
        [ObservableProperty] private OdontogramTool _selectedTool = OdontogramTool.Cursor;
        [ObservableProperty] private bool? _dialogResult;

        private int? _startToothForBridge = null;

        public OdontogramViewModel(IDialogService dialogService, IPdfService pdfService)
        {
            _dialogService = dialogService;
            _pdfService = pdfService;
            WeakReferenceMessenger.Default.Register<SurfaceClickedMessage>(this);
        }

        public void LoadState(ObservableCollection<ToothViewModel> masterOdontogram, ObservableCollection<DentalConnector> masterConnectors, Patient? currentPatient)
        {
            _currentPatient = currentPatient;
            Patient.FullName = currentPatient?.PatientDisplayInfo ?? "Paciente Desconocido";

            Odontogram.Clear();
            Connectors.Clear();

            // 1. Cargar Dientes
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

            // 2. Cargar Conectores
            if (masterConnectors != null)
            {
                foreach (var conn in masterConnectors)
                {
                    Connectors.Add(new DentalConnector
                    {
                        Type = conn.Type,
                        ColorHex = conn.ColorHex,
                        Thickness = conn.Thickness,
                        ToothSequence = new List<int>(conn.ToothSequence)
                    });
                }
            }

            UpdateSummary();
        }

        public void Receive(SurfaceClickedMessage message)
        {
            if (SelectedTool == OdontogramTool.Cursor) return;

            var tooth = Odontogram.FirstOrDefault(t => t.ToothNumber == message.ToothNumber);
            if (tooth == null) return;

            if (SelectedTool == OdontogramTool.Puente || SelectedTool == OdontogramTool.Ortodoncia)
            {
                HandleConnectorClick(tooth.ToothNumber);
                return;
            }
            _startToothForBridge = null;
            ApplyToolToTooth(tooth, message.Value);
        }

        private void HandleConnectorClick(int clickedTooth)
        {
            if (_startToothForBridge == null)
            {
                _startToothForBridge = clickedTooth;
                _dialogService.ShowMessage($"Inicio marcado en {clickedTooth}. Seleccione el final.", "Conector");
            }
            else
            {
                int start = _startToothForBridge.Value;
                int end = clickedTooth;

                if (start == end) { _startToothForBridge = null; return; }

                var sequence = CalculateToothSequence(start, end);
                if (sequence.Count > 1)
                {
                    var connector = new DentalConnector
                    {
                        Type = SelectedTool == OdontogramTool.Ortodoncia ? ConnectorType.Ortodoncia : ConnectorType.Puente,
                        ToothSequence = sequence,
                        ColorHex = SelectedTool == OdontogramTool.Ortodoncia ? "#2ECC71" : "#3498DB",
                        Thickness = 2.0
                    };
                    Connectors.Add(connector);
                    UpdateSummary();
                }
                _startToothForBridge = null;
            }
        }

        private List<int> CalculateToothSequence(int start, int end)
        {
            List<int> upperArch = new List<int> { 18, 17, 16, 15, 14, 13, 12, 11, 21, 22, 23, 24, 25, 26, 27, 28 };
            List<int> lowerArch = new List<int> { 48, 47, 46, 45, 44, 43, 42, 41, 31, 32, 33, 34, 35, 36, 37, 38 };

            List<int>? activeArch = null;
            if (upperArch.Contains(start) && upperArch.Contains(end)) activeArch = upperArch;
            else if (lowerArch.Contains(start) && lowerArch.Contains(end)) activeArch = lowerArch;
            else
            {
                _dialogService.ShowMessage("No se pueden conectar arcadas diferentes.", "Error");
                return new List<int>();
            }

            int idx1 = activeArch.IndexOf(start);
            int idx2 = activeArch.IndexOf(end);
            int min = Math.Min(idx1, idx2);
            int count = Math.Abs(idx1 - idx2) + 1;

            return activeArch.GetRange(min, count);
        }

        private void ApplyToolToTooth(ToothViewModel tooth, ToothSurface surface)
        {
            if (IsGlobalTool(SelectedTool))
            {
                ResetToothToHealthy(tooth);
                switch (SelectedTool)
                {
                    case OdontogramTool.Ausente: tooth.FullCondition = ToothCondition.Ausente; ApplyConditionToAllSurfaces(tooth, ToothCondition.Ausente); break;
                    case OdontogramTool.Extraccion: tooth.FullCondition = ToothCondition.ExtraccionIndicada; break;
                    case OdontogramTool.Corona: tooth.FullRestoration = ToothRestoration.Corona; ApplyRestorationToAllSurfaces(tooth, ToothRestoration.Corona); break;
                    case OdontogramTool.Implante: tooth.FullRestoration = ToothRestoration.Implante; ApplyRestorationToAllSurfaces(tooth, ToothRestoration.Implante); break;
                    case OdontogramTool.Endodoncia: tooth.FullRestoration = ToothRestoration.Endodoncia; break;
                }
            }
            else
            {
                if (tooth.FullCondition == ToothCondition.Ausente || IsFullRestoration(tooth.FullRestoration)) ResetToothToHealthy(tooth);
                switch (SelectedTool)
                {
                    case OdontogramTool.Caries: UpdateSurfaceCondition(tooth, surface, ToothCondition.Caries); break;
                    case OdontogramTool.Fractura: UpdateSurfaceCondition(tooth, surface, ToothCondition.Fractura); break;
                    case OdontogramTool.Obturacion: UpdateSurfaceRestoration(tooth, surface, ToothRestoration.Obturacion); break;
                }
            }
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            SummaryList.Clear();
            foreach (var c in Connectors)
            {
                string name = c.Type == ConnectorType.Puente ? "Puente" : "Ortodoncia";
                string color = c.Type == ConnectorType.Puente ? "#3498DB" : "#2ECC71";
                SummaryList.Add(new ToothStateSummary { ToothNumber = 0, Description = $"{name} ({c.ToothSequence.First()}-{c.ToothSequence.Last()})", Type = "Aparato", ColorIndicator = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)) });
            }
            foreach (var t in Odontogram.OrderBy(x => x.ToothNumber))
            {
                if (t.FullCondition == ToothCondition.Ausente) AddSummary(t.ToothNumber, "Ausente", "Estado", Brushes.Gray);
                else if (t.FullCondition == ToothCondition.ExtraccionIndicada) AddSummary(t.ToothNumber, "Extracción", "Condición", Brushes.Red);
                if (t.FullRestoration == ToothRestoration.Endodoncia) AddSummary(t.ToothNumber, "Endodoncia", "Tratamiento", Brushes.Purple);
                else if (t.FullRestoration == ToothRestoration.Implante) AddSummary(t.ToothNumber, "Implante", "Tratamiento", Brushes.SlateGray);
                else if (t.FullRestoration == ToothRestoration.Corona) AddSummary(t.ToothNumber, "Corona", "Tratamiento", Brushes.Gold);
                CheckFace(t.ToothNumber, "Oclusal", t.OclusalCondition, t.OclusalRestoration);
                CheckFace(t.ToothNumber, "Mesial", t.MesialCondition, t.MesialRestoration);
                CheckFace(t.ToothNumber, "Distal", t.DistalCondition, t.DistalRestoration);
                CheckFace(t.ToothNumber, "Vestibular", t.VestibularCondition, t.VestibularRestoration);
                CheckFace(t.ToothNumber, "Lingual", t.LingualCondition, t.LingualRestoration);
            }
        }

        private void CheckFace(int num, string face, ToothCondition cond, ToothRestoration rest)
        {
            if (cond == ToothCondition.Caries) AddSummary(num, $"Caries {face}", "Patología", Brushes.Red);
            if (rest == ToothRestoration.Obturacion) AddSummary(num, $"Obturación {face}", "Tratamiento", Brushes.RoyalBlue);
        }
        private void AddSummary(int tooth, string desc, string type, Brush color) => SummaryList.Add(new ToothStateSummary { ToothNumber = tooth, Description = desc, Type = type, ColorIndicator = color });
        [RelayCommand] private void SelectTool(string toolName) { if (Enum.TryParse<OdontogramTool>(toolName, true, out var t)) SelectedTool = t; _startToothForBridge = null; }
        [RelayCommand] private void ClearAllConnectors() { if (_dialogService.ShowConfirmation("¿Borrar puentes?", "Confirma") == CoreDialogResult.Yes) { Connectors.Clear(); UpdateSummary(); } }
        [RelayCommand] private void Accept() => DialogResult = true;
        [RelayCommand] private void Cancel() => DialogResult = false;

        [RelayCommand]
        private async Task Print()
        {
            if (_currentPatient == null) return;
            // Generamos el string serializado CORRECTAMENTE antes de enviarlo al PDF
            string path = await _pdfService.GenerateOdontogramPdfAsync(_currentPatient, GetSerializedState());
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        // --- CORRECCIÓN PRINCIPAL AQUÍ ---
        public string GetSerializedState()
        {
            try
            {
                // Mapeamos manualmente los ViewModel a los DTOs
                // Esto asegura que propiedades como ToothNumber (que es ReadOnly en el ViewModel)
                // se copien y serialicen correctamente en el JSON.
                var teethDtos = Odontogram.Select(t => new ToothStateDto
                {
                    ToothNumber = t.ToothNumber,
                    FullCondition = t.FullCondition,
                    OclusalCondition = t.OclusalCondition,
                    MesialCondition = t.MesialCondition,
                    DistalCondition = t.DistalCondition,
                    VestibularCondition = t.VestibularCondition,
                    LingualCondition = t.LingualCondition,
                    FullRestoration = t.FullRestoration,
                    OclusalRestoration = t.OclusalRestoration,
                    MesialRestoration = t.MesialRestoration,
                    DistalRestoration = t.DistalRestoration,
                    VestibularRestoration = t.VestibularRestoration,
                    LingualRestoration = t.LingualRestoration
                }).ToList();

                var wrapper = new OdontogramPersistenceWrapper
                {
                    Teeth = teethDtos,
                    Connectors = Connectors.ToList()
                };

                return JsonSerializer.Serialize(wrapper);
            }
            catch
            {
                return "";
            }
        }

        private bool IsGlobalTool(OdontogramTool t) => t == OdontogramTool.Ausente || t == OdontogramTool.Extraccion || t == OdontogramTool.Corona || t == OdontogramTool.Implante || t == OdontogramTool.Endodoncia || t == OdontogramTool.Borrador;
        private bool IsFullRestoration(ToothRestoration r) => r == ToothRestoration.Corona || r == ToothRestoration.Implante || r == ToothRestoration.Endodoncia;
        private void ResetToothToHealthy(ToothViewModel t) { t.FullCondition = ToothCondition.Sano; t.FullRestoration = ToothRestoration.Ninguna; ApplyConditionToAllSurfaces(t, ToothCondition.Sano); ApplyRestorationToAllSurfaces(t, ToothRestoration.Ninguna); }
        private void ApplyConditionToAllSurfaces(ToothViewModel t, ToothCondition c) { t.OclusalCondition = c; t.MesialCondition = c; t.DistalCondition = c; t.VestibularCondition = c; t.LingualCondition = c; }
        private void ApplyRestorationToAllSurfaces(ToothViewModel t, ToothRestoration r) { t.OclusalRestoration = r; t.MesialRestoration = r; t.DistalRestoration = r; t.VestibularRestoration = r; t.LingualRestoration = r; }
        private void UpdateSurfaceCondition(ToothViewModel t, ToothSurface s, ToothCondition c) { UpdateSurfaceRestoration(t, s, ToothRestoration.Ninguna); ApplyToSurface(t, s, (tooth) => SetCondition(tooth, s, c)); }
        private void UpdateSurfaceRestoration(ToothViewModel t, ToothSurface s, ToothRestoration r) { if (r != ToothRestoration.Ninguna) UpdateSurfaceCondition(t, s, ToothCondition.Sano); ApplyToSurface(t, s, (tooth) => SetRestoration(tooth, s, r)); }
        private void ApplyToSurface(ToothViewModel t, ToothSurface s, Action<ToothViewModel> action) { if (s == ToothSurface.Completo) { ApplyConditionToAllSurfaces(t, ToothCondition.Sano); ApplyRestorationToAllSurfaces(t, ToothRestoration.Ninguna); } action(t); }
        private void SetCondition(ToothViewModel t, ToothSurface s, ToothCondition c) { switch (s) { case ToothSurface.Oclusal: t.OclusalCondition = c; break; case ToothSurface.Mesial: t.MesialCondition = c; break; case ToothSurface.Distal: t.DistalCondition = c; break; case ToothSurface.Vestibular: t.VestibularCondition = c; break; case ToothSurface.Lingual: t.LingualCondition = c; break; case ToothSurface.Completo: t.FullCondition = c; ApplyConditionToAllSurfaces(t, c); break; } }
        private void SetRestoration(ToothViewModel t, ToothSurface s, ToothRestoration r) { switch (s) { case ToothSurface.Oclusal: t.OclusalRestoration = r; break; case ToothSurface.Mesial: t.MesialRestoration = r; break; case ToothSurface.Distal: t.DistalRestoration = r; break; case ToothSurface.Vestibular: t.VestibularRestoration = r; break; case ToothSurface.Lingual: t.LingualRestoration = r; break; case ToothSurface.Completo: t.FullRestoration = r; ApplyRestorationToAllSurfaces(t, r); break; } }
    }
}