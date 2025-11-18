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
using System.Windows.Media;

namespace TuClinica.UI.ViewModels
{
    // DTO para el resumen lateral
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
        private readonly IFileDialogService _fileDialogService;

        private Patient? _currentPatient;

        [ObservableProperty]
        private PatientDisplayModel _patient = new();

        public ObservableCollection<ToothViewModel> Odontogram { get; } = new();

        [ObservableProperty]
        private ObservableCollection<ToothStateSummary> _summaryList = new();

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
            UpdateSummary();
        }

        public void Receive(SurfaceClickedMessage message)
        {
            var tooth = Odontogram.FirstOrDefault(t => t.ToothNumber == message.ToothNumber);
            if (tooth == null) return;
            Application.Current.Dispatcher.Invoke(() => OpenStateDialog(tooth, message.Value));
        }

        private void OpenStateDialog(ToothViewModel tooth, ToothSurface surface)
        {
            var dialog = new OdontogramStateDialog();

            (ToothCondition currentCond, ToothRestoration currentRest) = GetSurfaceState(tooth, surface);

            // Pre-cargar estado actual en el diálogo
            if (tooth.FullCondition == ToothCondition.Ausente)
            {
                currentCond = ToothCondition.Ausente;
            }
            else if (IsFullRestoration(tooth.FullRestoration))
            {
                currentRest = tooth.FullRestoration;
            }

            dialog.LoadState(tooth.ToothNumber, surface, currentCond, currentRest);

            Window? owner = Application.Current.Windows.OfType<OdontogramWindow>().FirstOrDefault();
            if (owner != null) dialog.Owner = owner;

            if (dialog.ShowDialog() == true)
            {
                ApplyChangesToTooth(tooth, surface, dialog.NewCondition, dialog.NewRestoration);
            }
        }

        // --- LÓGICA PRINCIPAL CORREGIDA ---
        private void ApplyChangesToTooth(ToothViewModel tooth, ToothSurface surface, ToothCondition newCond, ToothRestoration newRest)
        {
            // 1. PRIORIDAD MÁXIMA: RESTAURACIÓN COMPLETA (IMPLANTE, CORONA, ETC.)
            // Si eliges "Implante", esto gana a cualquier estado "Ausente" anterior.
            if (IsFullRestoration(newRest))
            {
                // Limpiamos todo para evitar conflictos
                ResetToothToHealthy(tooth);

                // Aplicamos la restauración
                tooth.FullRestoration = newRest;
                ApplyRestorationToAllSurfaces(tooth, newRest);

                // Opcional: Si el usuario marcó también una patología (ej. Fractura en corona), la aplicamos.
                // PERO ignoramos "Ausente" porque un implante ocupa el lugar.
                if (newCond != ToothCondition.Sano && newCond != ToothCondition.Ausente)
                {
                    tooth.FullCondition = newCond;
                    ApplyConditionToAllSurfaces(tooth, newCond);
                }

                UpdateSummary();
                return;
            }

            // 2. PRIORIDAD ALTA: DIENTE AUSENTE
            // Si marcas "Ausente", borramos cualquier tratamiento previo.
            if (newCond == ToothCondition.Ausente)
            {
                ResetToothToHealthy(tooth); // Borra implantes o caries previos

                tooth.FullCondition = ToothCondition.Ausente;
                ApplyConditionToAllSurfaces(tooth, ToothCondition.Ausente);

                UpdateSummary();
                return;
            }

            // 3. EDICIÓN ESTÁNDAR (CARIES, OBTURACIONES, SANO)
            // Si llegamos aquí, NO es Implante NI Ausente.

            // A. Si el diente ESTABA "Ausente" o tenía "Implante", debemos limpiarlo
            // para poder aplicar el nuevo estado (ej. volver a Sano o poner una Caries).
            if (tooth.FullCondition == ToothCondition.Ausente || IsFullRestoration(tooth.FullRestoration))
            {
                ResetToothToHealthy(tooth);
            }

            // B. Aplicar cambios
            if (surface == ToothSurface.Completo)
            {
                // Cambio global
                tooth.FullCondition = newCond;
                tooth.FullRestoration = newRest;
                ApplyConditionToAllSurfaces(tooth, newCond);
                ApplyRestorationToAllSurfaces(tooth, newRest);
            }
            else
            {
                // Cambio solo en una cara
                UpdateSurface(tooth, surface, newCond, newRest);
            }

            UpdateSummary();
        }

        private void UpdateSummary()
        {
            SummaryList.Clear();
            var teeth = Odontogram.OrderBy(t => t.ToothNumber);

            foreach (var t in teeth)
            {
                // --- Resumen: Ausente ---
                if (t.FullCondition == ToothCondition.Ausente)
                {
                    AddSummary(t.ToothNumber, "Diente Ausente", "Condición", Brushes.Black);
                    continue;
                }

                // --- Resumen: Restauración Completa (Implante, etc.) ---
                if (IsFullRestoration(t.FullRestoration))
                {
                    Brush color = Brushes.Gold; // Color por defecto
                    if (t.FullRestoration == ToothRestoration.Implante) color = new SolidColorBrush(Color.FromRgb(149, 165, 166)); // Gris
                    else if (t.FullRestoration == ToothRestoration.Endodoncia) color = new SolidColorBrush(Color.FromRgb(155, 89, 182)); // Morado

                    AddSummary(t.ToothNumber, t.FullRestoration.ToString(), "Restauración", color);

                    // Si tiene además una patología (ej. Fractura) la mostramos
                    if (t.FullCondition == ToothCondition.Fractura)
                        AddSummary(t.ToothNumber, "Fractura", "Condición", Brushes.Orange);

                    continue; // No listamos caras si es completo
                }

                // --- Resumen: Extracción Indicada ---
                if (t.FullCondition == ToothCondition.ExtraccionIndicada)
                {
                    AddSummary(t.ToothNumber, "Extracción Indicada", "Condición", Brushes.OrangeRed);
                }

                // --- Resumen Detallado por Caras ---
                CheckSurfaceCondition(t.ToothNumber, "Oclusal", t.OclusalCondition);
                CheckSurfaceCondition(t.ToothNumber, "Mesial", t.MesialCondition);
                CheckSurfaceCondition(t.ToothNumber, "Distal", t.DistalCondition);
                CheckSurfaceCondition(t.ToothNumber, "Vestibular", t.VestibularCondition);
                CheckSurfaceCondition(t.ToothNumber, "Lingual", t.LingualCondition);

                if (t.FullRestoration == ToothRestoration.Ninguna)
                {
                    CheckSurfaceRestoration(t.ToothNumber, "Oclusal", t.OclusalRestoration);
                    CheckSurfaceRestoration(t.ToothNumber, "Mesial", t.MesialRestoration);
                    CheckSurfaceRestoration(t.ToothNumber, "Distal", t.DistalRestoration);
                    CheckSurfaceRestoration(t.ToothNumber, "Vestibular", t.VestibularRestoration);
                    CheckSurfaceRestoration(t.ToothNumber, "Lingual", t.LingualRestoration);
                }
            }
        }

        private void CheckSurfaceCondition(int toothNum, string surface, ToothCondition cond)
        {
            if (cond == ToothCondition.Caries)
                AddSummary(toothNum, $"Caries ({surface})", "Condición", Brushes.Red);
            else if (cond == ToothCondition.Fractura)
                AddSummary(toothNum, $"Fractura ({surface})", "Condición", Brushes.Orange);
        }

        private void CheckSurfaceRestoration(int toothNum, string surface, ToothRestoration rest)
        {
            if (rest == ToothRestoration.Obturacion)
                AddSummary(toothNum, $"Obturación ({surface})", "Restauración", Brushes.RoyalBlue);
            else if (rest == ToothRestoration.Sellador)
                AddSummary(toothNum, $"Sellador ({surface})", "Restauración", Brushes.LightGreen);
        }

        private void AddSummary(int tooth, string desc, string type, Brush color)
        {
            SummaryList.Add(new ToothStateSummary
            {
                ToothNumber = tooth,
                Description = desc,
                Type = type,
                ColorIndicator = color
            });
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

        private void UpdateSurface(ToothViewModel tooth, ToothSurface surface, ToothCondition cond, ToothRestoration rest)
        {
            switch (surface)
            {
                case ToothSurface.Oclusal: tooth.OclusalCondition = cond; tooth.OclusalRestoration = rest; break;
                case ToothSurface.Mesial: tooth.MesialCondition = cond; tooth.MesialRestoration = rest; break;
                case ToothSurface.Distal: tooth.DistalCondition = cond; tooth.DistalRestoration = rest; break;
                case ToothSurface.Vestibular: tooth.VestibularCondition = cond; tooth.VestibularRestoration = rest; break;
                case ToothSurface.Lingual: tooth.LingualCondition = cond; tooth.LingualRestoration = rest; break;
            }
        }

        private bool IsFullRestoration(ToothRestoration r)
        {
            return r == ToothRestoration.Corona ||
                   r == ToothRestoration.Implante ||
                   r == ToothRestoration.Endodoncia ||
                   r == ToothRestoration.ProtesisFija ||
                   r == ToothRestoration.ProtesisRemovible;
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

        [RelayCommand]
        private void Accept() => DialogResult = true;

        [RelayCommand]
        private void Cancel() => DialogResult = false;

        [RelayCommand]
        private async Task Print()
        {
            if (_currentPatient == null) return;
            try
            {
                string jsonState = GetSerializedState();
                string path = await _pdfService.GenerateOdontogramPdfAsync(_currentPatient, jsonState);
                if (_dialogService.ShowConfirmation($"PDF generado: {path}\n¿Abrir?", "Éxito") == CoreDialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error PDF: {ex.Message}", "Error");
            }
        }

        public string GetSerializedState()
        {
            try { return JsonSerializer.Serialize(Odontogram); }
            catch { return string.Empty; }
        }
    }
}