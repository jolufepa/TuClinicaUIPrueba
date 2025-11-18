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

            foreach (var tooth in masterOdontogram)
            {
                // Clonación manual para edición aislada
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

            // Forzamos ejecución en UI Thread
            Application.Current.Dispatcher.Invoke(() => OpenStateDialog(tooth, message.Value));
        }

        private void OpenStateDialog(ToothViewModel tooth, ToothSurface surface)
        {
            var dialog = new OdontogramStateDialog();

            (ToothCondition currentCond, ToothRestoration currentRest) = GetSurfaceState(tooth, surface);

            // Lógica de visualización inicial en el diálogo
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
                // Aplicamos los cambios inmediatamente
                ApplyChangesToTooth(tooth, surface, dialog.NewCondition, dialog.NewRestoration);
            }
        }

        private void ApplyChangesToTooth(ToothViewModel tooth, ToothSurface surface, ToothCondition newCond, ToothRestoration newRest)
        {
            // 1. SI ES AUSENTE
            if (newCond == ToothCondition.Ausente)
            {
                // Reset y marcar globalmente
                ResetToothToHealthy(tooth);
                tooth.FullCondition = ToothCondition.Ausente;
                // Propagar a las caras para asegurar que los triggers visuales reaccionen si es necesario
                ApplyConditionToAllSurfaces(tooth, ToothCondition.Ausente);
                return;
            }

            // 2. SI ES RESTAURACIÓN COMPLETA (Implante, Corona, Endodoncia...)
            if (IsFullRestoration(newRest))
            {
                ResetToothToHealthy(tooth);
                tooth.FullRestoration = newRest;

                // Si además hay una condición global (ej. Fractura), la aplicamos
                if (newCond != ToothCondition.Ausente && newCond != ToothCondition.Sano)
                {
                    tooth.FullCondition = newCond;
                    ApplyConditionToAllSurfaces(tooth, newCond);
                }
                return;
            }

            // 3. SI ES UN RESET (Volver a Sano)
            if (newCond == ToothCondition.Sano && newRest == ToothRestoration.Ninguna)
            {
                if (surface == ToothSurface.Completo)
                {
                    ResetToothToHealthy(tooth);
                }
                else
                {
                    // Si limpiamos una cara individual, pero el diente estaba "Ausente" o "Corona",
                    // debemos quitar ese estado global primero para poder editar la cara.
                    if (tooth.FullCondition == ToothCondition.Ausente || IsFullRestoration(tooth.FullRestoration))
                    {
                        ResetToothToHealthy(tooth);
                    }
                    UpdateSurface(tooth, surface, ToothCondition.Sano, ToothRestoration.Ninguna);
                }
                return;
            }

            // 4. EDICIÓN ESTÁNDAR (Parcial o Global)
            // Si estaba Ausente, lo recuperamos
            if (tooth.FullCondition == ToothCondition.Ausente)
            {
                tooth.FullCondition = ToothCondition.Sano;
                ApplyConditionToAllSurfaces(tooth, ToothCondition.Sano);
            }

            // Si tenía restauración completa, la quitamos
            if (IsFullRestoration(tooth.FullRestoration))
            {
                tooth.FullRestoration = ToothRestoration.Ninguna;
            }

            if (surface == ToothSurface.Completo)
            {
                // CRÍTICO: Para "Fractura" o "Extracción", debemos setear CADA superficie
                // para que el XAML (DataTriggers) sepa pintarlas todas.
                tooth.FullCondition = newCond;
                tooth.FullRestoration = newRest;

                ApplyConditionToAllSurfaces(tooth, newCond);
                ApplyRestorationToAllSurfaces(tooth, newRest);
            }
            else
            {
                // Edición de una sola cara
                UpdateSurface(tooth, surface, newCond, newRest);
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