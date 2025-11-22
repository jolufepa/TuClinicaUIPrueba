using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic; // Necesario para IEnumerable
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TuClinica.Core.Enums; // Necesario para el Enum
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.Core.Extensions;
using TuClinica.DataAccess;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;

namespace TuClinica.UI.ViewModels
{
    public partial class PatientInfoViewModel : BaseViewModel
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDialogService _dialogService;
        private readonly IValidationService _validationService;

        // Estado interno para edición (rollback)
        private Patient? _originalPatientState;

        [ObservableProperty]
        private Patient? _currentPatient;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SavePatientDataAsyncCommand))]
        private bool _isPatientDataReadOnly = true;

        // --- PROPIEDAD AÑADIDA PARA EL COMBOBOX ---
        public IEnumerable<PatientDocumentType> DocumentTypes => Enum.GetValues(typeof(PatientDocumentType)).Cast<PatientDocumentType>();

        public IRelayCommand ToggleEditPatientDataCommand { get; }
        public IAsyncRelayCommand SavePatientDataAsyncCommand { get; }

        public PatientInfoViewModel(
            IServiceScopeFactory scopeFactory,
            IDialogService dialogService,
            IValidationService validationService)
        {
            _scopeFactory = scopeFactory;
            _dialogService = dialogService;
            _validationService = validationService;

            ToggleEditPatientDataCommand = new RelayCommand(ToggleEditPatientData);
            SavePatientDataAsyncCommand = new AsyncRelayCommand(SavePatientDataAsync, CanSavePatientData);
        }

        public void Load(Patient patient)
        {
            IsPatientDataReadOnly = true;
            _originalPatientState = null;
            CurrentPatient = patient;
        }

        private bool CanSavePatientData()
        {
            return !IsPatientDataReadOnly && CurrentPatient != null && !CurrentPatient.HasErrors;
        }

        private void ToggleEditPatientData()
        {
            if (CurrentPatient == null) return;

            IsPatientDataReadOnly = !IsPatientDataReadOnly;

            if (!IsPatientDataReadOnly) // Entrando en modo edición
            {
                _originalPatientState = CurrentPatient.DeepCopy();
                CurrentPatient.ForceValidation();
            }
            else // Cancelando edición
            {
                if (_originalPatientState != null)
                {
                    CurrentPatient.CopyFrom(_originalPatientState);
                    _originalPatientState = null;
                }
            }
        }

        private async Task SavePatientDataAsync()
        {
            if (CurrentPatient == null || _originalPatientState == null) return;

            // --- VALIDACIONES ---
            CurrentPatient.ForceValidation();
            if (CurrentPatient.HasErrors)
            {
                _dialogService.ShowMessage($"Error: {CurrentPatient.GetErrors().FirstOrDefault()?.ErrorMessage}", "Validación");
                return;
            }

            CurrentPatient.Name = CurrentPatient.Name.ToTitleCase();
            CurrentPatient.Surname = CurrentPatient.Surname.ToTitleCase();
            CurrentPatient.DocumentNumber = CurrentPatient.DocumentNumber?.ToUpper().Trim() ?? string.Empty;
            CurrentPatient.Email = CurrentPatient.Email?.ToLower().Trim();
            if (string.IsNullOrEmpty(CurrentPatient.Email)) CurrentPatient.Email = null;

            if (!_validationService.IsValidDocument(CurrentPatient.DocumentNumber, CurrentPatient.DocumentType))
            {
                _dialogService.ShowMessage("Documento no válido.", "Error");
                return;
            }

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    bool docChanged = !string.Equals(_originalPatientState.DocumentNumber, CurrentPatient.DocumentNumber, StringComparison.OrdinalIgnoreCase) ||
                                      _originalPatientState.DocumentType != CurrentPatient.DocumentType;

                    bool proceedToUpdate = true;

                    if (docChanged)
                    {
                        // Detectar duplicado
                        var duplicate = await context.Patients
                            .FirstOrDefaultAsync(p => p.Id != CurrentPatient.Id &&
                                                 p.DocumentNumber.ToLower() == CurrentPatient.DocumentNumber.ToLower());

                        if (duplicate != null)
                        {
                            var result = _dialogService.ShowConfirmation(
                                $"El DNI/NIE ya existe en el paciente: {duplicate.Name} {duplicate.Surname} (ID: {duplicate.Id})\n\n" +
                                "¿CONFIRMAR FUSIÓN BLINDADA?\n" +
                                "El sistema buscará y moverá AUTOMÁTICAMENTE todos los registros vinculados de todas las tablas.",
                                "Fusión Definitiva");

                            if (result == CoreDialogResult.Yes)
                            {
                                // =======================================================
                                //  PROTOCOLO OMEGA: FUSIÓN POR REFLEXIÓN Y SQL DIRECTO
                                // =======================================================
                                // Esto encuentra CUALQUIER tabla con 'PatientId' y mueve los datos.
                                // Ya no importa si olvidamos 'PaymentAllocations' o 'ActivityLogs', esto las atrapa a todas.

                                var entityTypes = context.Model.GetEntityTypes();
                                int tablasMovidas = 0;

                                foreach (var type in entityTypes)
                                {
                                    // Ignoramos la tabla de Pacientes (no queremos mover el paciente a sí mismo)
                                    if (type.ClrType == typeof(TuClinica.Core.Models.Patient)) continue;

                                    // Buscamos si la tabla tiene la propiedad "PatientId"
                                    var patientIdProp = type.FindProperty("PatientId");
                                    if (patientIdProp != null)
                                    {
                                        // Obtenemos el nombre real de la tabla en la BD
                                        var tableName = type.GetTableName();
                                        if (!string.IsNullOrEmpty(tableName))
                                        {
                                            // EJECUCIÓN SQL DIRECTA (Rápida y sin chequeos de EF que bloqueen)
                                            // "UPDATE [Tabla] SET PatientId = [Nuevo] WHERE PatientId = [Viejo]"
                                            var sql = $"UPDATE \"{tableName}\" SET \"PatientId\" = {CurrentPatient.Id} WHERE \"PatientId\" = {duplicate.Id}";
                                            await context.Database.ExecuteSqlRawAsync(sql);
                                            tablasMovidas++;
                                        }
                                    }
                                }

                                // BORRADO FINAL DEL DUPLICADO (VÍA SQL PARA EVITAR BLOQUEOS)
                                await context.Database.ExecuteSqlRawAsync($"DELETE FROM \"Patients\" WHERE \"Id\" = {duplicate.Id}");

                                _dialogService.ShowMessage($"Fusión completada. Se han procesado {tablasMovidas} tablas del sistema.", "Éxito Total");
                            }
                            else
                            {
                                // Cancelar
                                CurrentPatient.DocumentNumber = _originalPatientState.DocumentNumber;
                                CurrentPatient.DocumentType = _originalPatientState.DocumentType;
                                proceedToUpdate = false;
                            }
                        }

                        // Archivado automático
                        if (proceedToUpdate && !string.IsNullOrWhiteSpace(_originalPatientState.DocumentNumber))
                        {
                            if (_originalPatientState.DocumentNumber.ToLower() != CurrentPatient.DocumentNumber.ToLower())
                            {
                                // Usamos SQL directo también aquí para asegurar consistencia inmediata
                                var note = $"Documento previo (Archivado el {DateTime.Now:dd/MM/yyyy})";
                                var sqlArchivado = $"INSERT INTO \"LinkedDocuments\" (\"PatientId\", \"DocumentType\", \"DocumentNumber\", \"Notes\") " +
                                                   $"VALUES ({CurrentPatient.Id}, {(int)_originalPatientState.DocumentType}, '{_originalPatientState.DocumentNumber}', '{note}')";
                                await context.Database.ExecuteSqlRawAsync(sqlArchivado);
                            }
                        }
                    }

                    if (proceedToUpdate)
                    {
                        // Guardado final de los datos del paciente actual
                        var patientToUpdate = await context.Patients.FindAsync(CurrentPatient.Id);
                        if (patientToUpdate != null)
                        {
                            context.Entry(patientToUpdate).CurrentValues.SetValues(CurrentPatient);
                            await context.SaveChangesAsync();

                            _dialogService.ShowMessage("Datos guardados correctamente.", "Éxito");
                            _originalPatientState = CurrentPatient.DeepCopy();
                            IsPatientDataReadOnly = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error Crítico: {ex.Message}\n{ex.InnerException?.Message}", "Error del Sistema");
            }
        }
    }
}