using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.DataAccess;
using TuClinica.UI.Messages; // Para OpenOdontogramMessage

namespace TuClinica.UI.ViewModels
{
    public partial class PatientFileViewModel : BaseViewModel
    {
        private readonly IDialogService _dialogService;
        private readonly IServiceScopeFactory _scopeFactory;

        // --- SUB-VIEWMODELS ---
        public PatientInfoViewModel PatientInfoVM { get; }
        public PatientDocumentsViewModel PatientDocumentsVM { get; }
        public PatientAlertsViewModel PatientAlertsVM { get; }
        public PatientFinancialViewModel PatientFinancialVM { get; }
        public PatientTreatmentPlanViewModel PatientTreatmentPlanVM { get; }
        public PatientOdontogramViewModel PatientOdontogramVM { get; }

        private CancellationTokenSource? _loadPatientCts;

        [ObservableProperty]
        private Patient? _currentPatient;

        [ObservableProperty] private bool _isLoading = false;

        // Constructor que coincide con App.xaml.cs (8 Argumentos)
        public PatientFileViewModel(
          IDialogService dialogService,
          IServiceScopeFactory scopeFactory,
          PatientInfoViewModel patientInfoVM,
          PatientDocumentsViewModel patientDocumentsVM,
          PatientAlertsViewModel patientAlertsVM,
          PatientFinancialViewModel patientFinancialVM,
          PatientTreatmentPlanViewModel patientTreatmentPlanVM,
          PatientOdontogramViewModel patientOdontogramVM)
        {
            _dialogService = dialogService;
            _scopeFactory = scopeFactory;

            PatientInfoVM = patientInfoVM;
            PatientDocumentsVM = patientDocumentsVM;
            PatientAlertsVM = patientAlertsVM;
            PatientFinancialVM = patientFinancialVM;
            PatientTreatmentPlanVM = patientTreatmentPlanVM;
            PatientOdontogramVM = patientOdontogramVM;
        }

        public async Task LoadPatient(Patient patient)
        {
            _loadPatientCts?.Cancel();
            _loadPatientCts = new CancellationTokenSource();
            var token = _loadPatientCts.Token;

            if (IsLoading) return;

            try
            {
                IsLoading = true;
                CurrentPatient = null;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    // Cargamos el paciente y sus documentos para que los sub-VMs los usen
                    var freshPatient = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                        Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(
                            Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include(context.Patients, p => p.LinkedDocuments)
                        ),
                        p => p.Id == patient.Id, token);

                    if (freshPatient == null) return;
                    token.ThrowIfCancellationRequested();

                    CurrentPatient = freshPatient;

                    // --- 1. Carga Síncrona de UI principal ---
                    PatientInfoVM.Load(CurrentPatient);
                    PatientDocumentsVM.Load(CurrentPatient);
                    PatientOdontogramVM.Load(CurrentPatient);

                    // --- 2. Carga Asíncrona Paralela (Datos pesados) ---
                    var alertsTask = PatientAlertsVM.LoadAsync(CurrentPatient, token);
                    var financeTask = PatientFinancialVM.LoadAsync(CurrentPatient, token);
                    var planTask = PatientTreatmentPlanVM.LoadAsync(CurrentPatient, token);

                    await Task.WhenAll(alertsTask, financeTask, planTask);

                    token.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException)
            {
                CurrentPatient = null;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al cargar ficha: {ex.Message}", "Error");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}