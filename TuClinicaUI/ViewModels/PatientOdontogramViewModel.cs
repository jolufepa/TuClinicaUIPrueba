using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.UI.Messages;
using TuClinica.UI.Views;

namespace TuClinica.UI.ViewModels
{
    public partial class PatientOdontogramViewModel : BaseViewModel
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDialogService _dialogService;
        private readonly IPdfService _pdfService;

        [ObservableProperty]
        private Patient? _currentPatient;

        // Colecciones visuales
        public ObservableCollection<ToothViewModel> Odontogram { get; } = new();
        public ObservableCollection<DentalConnector> MasterConnectors { get; } = new();

        [ObservableProperty]
        private OdontogramPreviewViewModel _odontogramPreviewVM = new();

        public IAsyncRelayCommand PrintOdontogramCommand { get; }

        public PatientOdontogramViewModel(
            IServiceScopeFactory scopeFactory,
            IDialogService dialogService,
            IPdfService pdfService)
        {
            _scopeFactory = scopeFactory;
            _dialogService = dialogService;
            _pdfService = pdfService;

            InitializeOdontogram();

            // Nos suscribimos al mensaje para abrir la ventana modal
            WeakReferenceMessenger.Default.Register<OpenOdontogramMessage>(this, (r, m) => OpenOdontogramWindow());

            PrintOdontogramCommand = new AsyncRelayCommand(PrintOdontogramAsync);
        }

        public void Load(Patient patient)
        {
            CurrentPatient = patient;
            LoadOdontogramStateFromJson();
            OdontogramPreviewVM.LoadFromMaster(this.Odontogram, this.MasterConnectors);
        }

        private void InitializeOdontogram()
        {
            Odontogram.Clear();
            for (int i = 18; i >= 11; i--) Odontogram.Add(new ToothViewModel(i));
            for (int i = 21; i <= 28; i++) Odontogram.Add(new ToothViewModel(i));
            for (int i = 41; i <= 48; i++) Odontogram.Add(new ToothViewModel(i));
            for (int i = 38; i >= 31; i--) Odontogram.Add(new ToothViewModel(i));
            MasterConnectors.Clear();
        }

        private void LoadOdontogramStateFromJson()
        {
            // Resetear estado visual
            foreach (var t in Odontogram)
            {
                t.FullCondition = ToothCondition.Sano; t.OclusalCondition = ToothCondition.Sano; t.MesialCondition = ToothCondition.Sano;
                t.DistalCondition = ToothCondition.Sano; t.VestibularCondition = ToothCondition.Sano; t.LingualCondition = ToothCondition.Sano;
                t.FullRestoration = ToothRestoration.Ninguna; t.OclusalRestoration = ToothRestoration.Ninguna; t.MesialRestoration = ToothRestoration.Ninguna;
                t.DistalRestoration = ToothRestoration.Ninguna; t.VestibularRestoration = ToothRestoration.Ninguna; t.LingualRestoration = ToothRestoration.Ninguna;
            }
            MasterConnectors.Clear();

            if (CurrentPatient == null || string.IsNullOrWhiteSpace(CurrentPatient.OdontogramStateJson)) return;

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var wrapper = JsonSerializer.Deserialize<OdontogramPersistenceWrapper>(CurrentPatient.OdontogramStateJson, options);

                if (wrapper != null && wrapper.SchemaVersion <= 1)
                {
                    if (wrapper.Teeth != null) ApplyTeethState(wrapper.Teeth);
                    if (wrapper.Connectors != null) foreach (var c in wrapper.Connectors) MasterConnectors.Add(c);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Error JSON: {ex.Message}"); }
        }

        private void ApplyTeethState(List<ToothStateDto> savedTeeth)
        {
            foreach (var s in savedTeeth)
            {
                var m = Odontogram.FirstOrDefault(t => t.ToothNumber == s.ToothNumber);
                if (m != null)
                {
                    m.FullCondition = s.FullCondition; m.OclusalCondition = s.OclusalCondition; m.MesialCondition = s.MesialCondition;
                    m.DistalCondition = s.DistalCondition; m.VestibularCondition = s.VestibularCondition; m.LingualCondition = s.LingualCondition;
                    m.FullRestoration = s.FullRestoration; m.OclusalRestoration = s.OclusalRestoration; m.MesialRestoration = s.MesialRestoration;
                    m.DistalRestoration = s.DistalRestoration; m.VestibularRestoration = s.VestibularRestoration; m.LingualRestoration = s.LingualRestoration;
                }
            }
        }

        private async void OpenOdontogramWindow()
        {
            if (CurrentPatient == null) return;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var vm = scope.ServiceProvider.GetRequiredService<OdontogramViewModel>();
                    var win = scope.ServiceProvider.GetRequiredService<OdontogramWindow>();

                    vm.LoadState(Odontogram, MasterConnectors, CurrentPatient);
                    win.DataContext = vm;

                    if (System.Windows.Application.Current.MainWindow != win)
                        win.Owner = System.Windows.Application.Current.MainWindow;

                    win.ShowDialog();

                    if (vm.DialogResult == true)
                    {
                        var json = vm.GetSerializedState();
                        if (CurrentPatient.OdontogramStateJson != json)
                        {
                            CurrentPatient.OdontogramStateJson = json;
                            await SavePatientOdontogramStateAsync();
                            LoadOdontogramStateFromJson();
                        }
                    }
                }
                OdontogramPreviewVM.LoadFromMaster(this.Odontogram, this.MasterConnectors);
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); }
        }

        private async Task SavePatientOdontogramStateAsync()
        {
            if (CurrentPatient == null) return;
            try { using (var s = _scopeFactory.CreateScope()) { var r = s.ServiceProvider.GetRequiredService<IPatientRepository>(); var p = await r.GetByIdAsync(CurrentPatient.Id); if (p != null) { p.OdontogramStateJson = CurrentPatient.OdontogramStateJson; await r.SaveChangesAsync(); } } }
            catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); }
        }

        private async Task PrintOdontogramAsync()
        {
            if (CurrentPatient == null) return;
            try
            {
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
                var wrapper = new OdontogramPersistenceWrapper { SchemaVersion = 1, Teeth = teethDtos, Connectors = MasterConnectors.ToList() };
                var jsonState = JsonSerializer.Serialize(wrapper);
                string path = await _pdfService.GenerateOdontogramPdfAsync(CurrentPatient, jsonState);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
        }
    }
}