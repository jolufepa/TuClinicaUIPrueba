using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;

namespace TuClinica.UI.ViewModels
{
    public partial class PatientTreatmentPlanViewModel : BaseViewModel
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private Patient? _currentPatient;

        // Colección de tareas
        public ObservableCollection<TreatmentPlanItem> PendingTasks { get; } = new();

        [ObservableProperty]
        private int _pendingTaskCount = 0;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddPlanItemAsyncCommand))]
        private string _newPlanItemDescription = string.Empty;

        // Comandos
        public IAsyncRelayCommand AddPlanItemAsyncCommand { get; }
        public IAsyncRelayCommand<TreatmentPlanItem> TogglePlanItemAsyncCommand { get; }
        public IAsyncRelayCommand<TreatmentPlanItem> DeletePlanItemAsyncCommand { get; }
        public IAsyncRelayCommand CheckPendingTasksCommand { get; }

        public PatientTreatmentPlanViewModel(
            IServiceScopeFactory scopeFactory,
            IDialogService dialogService)
        {
            _scopeFactory = scopeFactory;
            _dialogService = dialogService;

            AddPlanItemAsyncCommand = new AsyncRelayCommand(AddPlanItemAsync, CanAddPlanItem);
            TogglePlanItemAsyncCommand = new AsyncRelayCommand<TreatmentPlanItem>(TogglePlanItemAsync);
            DeletePlanItemAsyncCommand = new AsyncRelayCommand<TreatmentPlanItem>(DeletePlanItemAsync);
            CheckPendingTasksCommand = new AsyncRelayCommand(CheckPendingTasksAsync);
        }

        public async Task LoadAsync(Patient patient, CancellationToken token)
        {
            CurrentPatient = patient;
            NewPlanItemDescription = "";
            PendingTasks.Clear();

            using (var scope = _scopeFactory.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<ITreatmentPlanItemRepository>();
                var tasks = await repo.GetTasksForPatientAsync(patient.Id);

                token.ThrowIfCancellationRequested();

                foreach (var task in tasks.OrderBy(x => x.IsDone).ThenByDescending(x => x.DateAdded))
                {
                    PendingTasks.Add(task);
                }
            }

            UpdateCount();
        }

        private void UpdateCount()
        {
            PendingTaskCount = PendingTasks.Count(x => !x.IsDone);
        }

        private bool CanAddPlanItem() => !string.IsNullOrWhiteSpace(NewPlanItemDescription);

        private async Task AddPlanItemAsync()
        {
            if (CurrentPatient != null && CanAddPlanItem())
            {
                try
                {
                    using (var s = _scopeFactory.CreateScope())
                    {
                        var r = s.ServiceProvider.GetRequiredService<ITreatmentPlanItemRepository>();
                        var newItem = new TreatmentPlanItem
                        {
                            PatientId = CurrentPatient.Id,
                            Description = NewPlanItemDescription,
                            DateAdded = DateTime.Now
                        };
                        await r.AddAsync(newItem);
                        await r.SaveChangesAsync();

                        // Actualizar UI (Insertar al principio de los pendientes)
                        PendingTasks.Insert(0, newItem);
                        UpdateCount();
                    }
                    NewPlanItemDescription = "";
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage(ex.Message, "Error");
                }
            }
        }

        private async Task TogglePlanItemAsync(TreatmentPlanItem? item)
        {
            if (item != null)
            {
                try
                {
                    using (var s = _scopeFactory.CreateScope())
                    {
                        var r = s.ServiceProvider.GetRequiredService<ITreatmentPlanItemRepository>();
                        var dbItem = await r.GetByIdAsync(item.Id);
                        if (dbItem != null)
                        {
                            dbItem.IsDone = !dbItem.IsDone; // Invertir estado en BD
                            item.IsDone = dbItem.IsDone;    // Actualizar estado en UI
                            r.Update(dbItem);
                            await r.SaveChangesAsync();
                            UpdateCount();
                        }
                    }
                    // Reordenar visualmente podría ser complejo aquí, lo dejamos simple
                }
                catch { }
            }
        }

        private async Task DeletePlanItemAsync(TreatmentPlanItem? item)
        {
            if (item != null && _dialogService.ShowConfirmation("¿Borrar tarea?", "Confirmar") == CoreDialogResult.Yes)
            {
                try
                {
                    using (var s = _scopeFactory.CreateScope())
                    {
                        var r = s.ServiceProvider.GetRequiredService<ITreatmentPlanItemRepository>();
                        var dbItem = await r.GetByIdAsync(item.Id);
                        if (dbItem != null)
                        {
                            r.Remove(dbItem);
                            await r.SaveChangesAsync();
                        }
                    }
                    PendingTasks.Remove(item);
                    UpdateCount();
                }
                catch { }
            }
        }

        private async Task CheckPendingTasksAsync()
        {
            // Pequeño delay para asegurar carga UI
            await Task.Delay(50);
            if (PendingTaskCount > 0)
            {
                _dialogService.ShowMessage($"El paciente tiene {PendingTaskCount} tareas pendientes.", "Aviso Plan de Tratamiento");
            }
        }
    }
}