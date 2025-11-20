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

            CurrentPatient.ForceValidation();
            if (CurrentPatient.HasErrors)
            {
                var firstError = CurrentPatient.GetErrors().FirstOrDefault()?.ErrorMessage;
                _dialogService.ShowMessage($"Error de validación: {firstError}", "Datos Inválidos");
                return;
            }

            CurrentPatient.Name = CurrentPatient.Name.ToTitleCase();
            CurrentPatient.Surname = CurrentPatient.Surname.ToTitleCase();
            CurrentPatient.DocumentNumber = CurrentPatient.DocumentNumber?.ToUpper().Trim() ?? string.Empty;
            CurrentPatient.Email = CurrentPatient.Email?.ToLower().Trim();
            if (string.IsNullOrEmpty(CurrentPatient.Email)) CurrentPatient.Email = null;

            if (!_validationService.IsValidDocument(CurrentPatient.DocumentNumber, CurrentPatient.DocumentType))
            {
                _dialogService.ShowMessage("El número de documento no tiene un formato válido.", "Documento Inválido");
                return;
            }

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    bool docChanged = !string.Equals(_originalPatientState.DocumentNumber, CurrentPatient.DocumentNumber, StringComparison.OrdinalIgnoreCase) ||
                                      _originalPatientState.DocumentType != CurrentPatient.DocumentType;

                    if (docChanged)
                    {
                        var duplicate = await context.Patients.AsNoTracking()
                            .FirstOrDefaultAsync(p => p.Id != CurrentPatient.Id && p.DocumentNumber.ToLower() == CurrentPatient.DocumentNumber.ToLower());

                        if (duplicate == null)
                        {
                            var linkedMatch = await context.LinkedDocuments.AsNoTracking().Include(d => d.Patient)
                                .FirstOrDefaultAsync(d => d.PatientId != CurrentPatient.Id && d.DocumentNumber.ToLower() == CurrentPatient.DocumentNumber.ToLower());
                            if (linkedMatch != null) duplicate = linkedMatch.Patient;
                        }

                        if (duplicate != null)
                        {
                            _dialogService.ShowMessage($"El documento ya existe en el paciente: {duplicate.PatientDisplayInfo}", "Duplicado");
                            CurrentPatient.DocumentNumber = _originalPatientState.DocumentNumber;
                            CurrentPatient.DocumentType = _originalPatientState.DocumentType;
                            return;
                        }

                        if (!string.IsNullOrWhiteSpace(_originalPatientState.DocumentNumber))
                        {
                            var oldDoc = new LinkedDocument
                            {
                                PatientId = CurrentPatient.Id,
                                DocumentType = _originalPatientState.DocumentType,
                                DocumentNumber = _originalPatientState.DocumentNumber,
                                Notes = $"Archivado el {DateTime.Now:dd/MM/yy}"
                            };
                            context.LinkedDocuments.Add(oldDoc);
                        }
                    }

                    var patientToUpdate = await context.Patients.FindAsync(CurrentPatient.Id);
                    if (patientToUpdate != null)
                    {
                        context.Entry(patientToUpdate).CurrentValues.SetValues(CurrentPatient);
                        await context.SaveChangesAsync();

                        _dialogService.ShowMessage("Datos actualizados.", "Éxito");
                        _originalPatientState = CurrentPatient.DeepCopy();
                        IsPatientDataReadOnly = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al guardar: {ex.Message}", "Error");
            }
        }
    }
}