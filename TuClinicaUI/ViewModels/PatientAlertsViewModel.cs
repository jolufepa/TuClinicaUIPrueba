using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;

namespace TuClinica.UI.ViewModels
{
    public partial class PatientAlertsViewModel : BaseViewModel
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private Patient? _currentPatient;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasActiveAlerts))]
        private ObservableCollection<PatientAlert> _activeAlerts = new();
        public bool HasActiveAlerts => ActiveAlerts.Any();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddAlertCommand))]
        private string _newAlertMessage = string.Empty;

        [ObservableProperty]
        private AlertLevel _newAlertLevel = AlertLevel.Warning;

        public IAsyncRelayCommand AddAlertCommand { get; }
        public IAsyncRelayCommand<PatientAlert> DeleteAlertCommand { get; }

        public IEnumerable<AlertLevel> AlertLevels => Enum.GetValues(typeof(AlertLevel)).Cast<AlertLevel>();

        public PatientAlertsViewModel(
            IServiceScopeFactory scopeFactory,
            IDialogService dialogService)
        {
            _scopeFactory = scopeFactory;
            _dialogService = dialogService;

            AddAlertCommand = new AsyncRelayCommand(AddAlertAsync, CanAddAlert);
            DeleteAlertCommand = new AsyncRelayCommand<PatientAlert>(DeleteAlertAsync);
        }

        public async Task LoadAsync(Patient patient, CancellationToken token)
        {
            CurrentPatient = patient;
            ActiveAlerts.Clear();
            NewAlertMessage = "";

            using (var scope = _scopeFactory.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPatientAlertRepository>();
                var alerts = await repo.GetActiveAlertsForPatientAsync(patient.Id, token);

                foreach (var a in alerts)
                {
                    ActiveAlerts.Add(a);
                }
            }

            OnPropertyChanged(nameof(HasActiveAlerts));
            ShowCriticalAlertsPopup();
        }

        private void ShowCriticalAlertsPopup()
        {
            if (ActiveAlerts.Any(a => a.Level == AlertLevel.Critical))
            {
                var msg = string.Join("\n", ActiveAlerts.Where(a => a.Level == AlertLevel.Critical).Select(a => "- " + a.Message));
                // Usamos el Dispatcher por si acaso se llama desde un hilo no-UI, aunque en MVVM suele ser directo.
                Application.Current.Dispatcher.Invoke(() =>
                    _dialogService.ShowMessage($"¡ALERTA CRÍTICA!\n\n{msg}", "ALERTA"));
            }
        }

        private bool CanAddAlert() => !string.IsNullOrWhiteSpace(NewAlertMessage);

        private async Task AddAlertAsync()
        {
            if (CurrentPatient != null && CanAddAlert())
            {
                var alert = new PatientAlert
                {
                    PatientId = CurrentPatient.Id,
                    Message = NewAlertMessage,
                    Level = NewAlertLevel,
                    IsActive = true
                };

                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var repo = scope.ServiceProvider.GetRequiredService<IPatientAlertRepository>();
                        await repo.AddAsync(alert);
                        await repo.SaveChangesAsync();
                    }
                    ActiveAlerts.Add(alert);
                    NewAlertMessage = "";
                    OnPropertyChanged(nameof(HasActiveAlerts));
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage(ex.Message, "Error");
                }
            }
        }

        private async Task DeleteAlertAsync(PatientAlert? alert)
        {
            if (alert != null && _dialogService.ShowConfirmation("¿Borrar alerta?", "Confirmar") == CoreDialogResult.Yes)
            {
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var repo = scope.ServiceProvider.GetRequiredService<IPatientAlertRepository>();
                        var db = await repo.GetByIdAsync(alert.Id);
                        if (db != null)
                        {
                            repo.Remove(db);
                            await repo.SaveChangesAsync();
                        }
                    }
                    ActiveAlerts.Remove(alert);
                    OnPropertyChanged(nameof(HasActiveAlerts));
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage(ex.Message, "Error");
                }
            }
        }
    }
}