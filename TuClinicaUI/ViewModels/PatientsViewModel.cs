// En: TuClinicaUI/ViewModels/PatientsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TuClinica.Core.Enums;
using TuClinica.Core.Extensions;
using TuClinica.Core.Interfaces;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.UI.Views;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;

namespace TuClinica.UI.ViewModels
{
    public partial class PatientsViewModel : BaseViewModel
    {
        // --- INICIO DE MODIFICACIÓN: Propiedades de Paginación ---
        private const int PageSize = 25; // 25 pacientes por página

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
        private int _currentPage = 1;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
        private int _totalPages = 1;

        [ObservableProperty]
        private string _pageInfo = "Página 1 de 1";
        

        private readonly IPatientRepository _patientRepository;
        private readonly IValidationService _validationService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly PatientFileViewModel _patientFileViewModel;
        private readonly IActivityLogService _activityLogService;
        private readonly IDialogService _dialogService;
        private ICommand? _navigateToPatientFileCommand;

        private CancellationTokenSource? _searchCts;

        [ObservableProperty]
        private ObservableCollection<Patient> _patients = new ObservableCollection<Patient>();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPatientSelected))]
        [NotifyPropertyChangedFor(nameof(IsPatientSelectedAndActive))]
        [NotifyPropertyChangedFor(nameof(IsPatientSelectedAndInactive))]
        // --- MODIFICACIÓN: Eliminada notificación de comando borrado ---
        private Patient? _selectedPatient;

        [ObservableProperty]
        private Patient _patientFormModel = new Patient();

        [ObservableProperty]
        private bool _isFormEnabled = false;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _showInactivePatients = false;

        public bool IsPatientSelected => SelectedPatient != null;
        public bool IsPatientSelectedAndActive => SelectedPatient != null && SelectedPatient.IsActive;
        public bool IsPatientSelectedAndInactive => SelectedPatient != null && !SelectedPatient.IsActive;


        // --- Comandos Manuales ---
        public IAsyncRelayCommand SearchPatientsCommand { get; }
        public IRelayCommand SetNewPatientFormCommand { get; }
        public IRelayCommand EditPatientCommand { get; }
        public IAsyncRelayCommand SavePatientCommand { get; }
        public IAsyncRelayCommand DeletePatientAsyncCommand { get; } // <-- Este será nuestro "Botón Inteligente"
        public IRelayCommand CreatePrescriptionCommand { get; }
        public IAsyncRelayCommand ViewPatientDetailsCommand { get; }
        public IAsyncRelayCommand ReactivatePatientAsyncCommand { get; }
        // --- MODIFICACIÓN: Comando eliminado ---
        // public IAsyncRelayCommand DeletePatientPermanentlyAsyncCommand { get; }


        // --- INICIO DE MODIFICACIÓN: Comandos de Paginación ---
        public IAsyncRelayCommand NextPageCommand { get; }
        public IAsyncRelayCommand PreviousPageCommand { get; }
        
        public IEnumerable<PatientDocumentType> DocumentTypes => Enum.GetValues(typeof(PatientDocumentType)).Cast<PatientDocumentType>();

        public PatientsViewModel(IPatientRepository patientRepository,
                                 IValidationService validationService,
                                 IServiceScopeFactory scopeFactory,
                                 PatientFileViewModel patientFileViewModel,
                                 IActivityLogService activityLogService,
                                 IDialogService dialogService)
        {
            _patientRepository = patientRepository;
            _validationService = validationService;
            _scopeFactory = scopeFactory;
            _patientFileViewModel = patientFileViewModel;
            _activityLogService = activityLogService;
            _dialogService = dialogService;

            // Inicialización de comandos
            SearchPatientsCommand = new AsyncRelayCommand(() => LoggedSearchAsync(true)); // Forzar reseteo de página
            SetNewPatientFormCommand = new RelayCommand(SetNewPatientForm);
            EditPatientCommand = new RelayCommand(EditPatient, () => IsPatientSelected);
            SavePatientCommand = new AsyncRelayCommand(SavePatientAsync);
            DeletePatientAsyncCommand = new AsyncRelayCommand(DeletePatientAsync, () => IsPatientSelectedAndActive);
            ReactivatePatientAsyncCommand = new AsyncRelayCommand(ReactivatePatientAsync, () => IsPatientSelectedAndInactive);
            CreatePrescriptionCommand = new RelayCommand(CreatePrescription, () => IsPatientSelected);
            ViewPatientDetailsCommand = new AsyncRelayCommand(ViewPatientDetailsAsync, () => IsPatientSelected);

            // --- MODIFICACIÓN: Comando eliminado ---
            // DeletePatientPermanentlyAsyncCommand = new AsyncRelayCommand(DeletePatientPermanentlyAsync, () => IsPatientSelectedAndInactive);

            // --- INICIO DE MODIFICACIÓN: Inicialización Comandos Paginación ---
            NextPageCommand = new AsyncRelayCommand(NextPageAsync, CanGoNext);
            PreviousPageCommand = new AsyncRelayCommand(PreviousPageAsync, CanGoPrevious);
            

            _ = SearchPatientsAsync(); // Carga inicial
        }

        public void SetNavigationCommand(ICommand navigationCommand)
        {
            _navigateToPatientFileCommand = navigationCommand;
        }

        private async Task ViewPatientDetailsAsync()
        {
            if (SelectedPatient == null) return;
            if (_navigateToPatientFileCommand == null)
            {
                _dialogService.ShowMessage("Error de navegación. El comando no está configurado.", "Error");
                return;
            }

            try
            {
                _activityLogService.LogAccessAsync(
                    entityType: "Patient",
                    entityId: SelectedPatient.Id,
                    details: $"Vio la ficha de: {SelectedPatient.PatientDisplayInfo}");

                await _patientFileViewModel.LoadPatient(SelectedPatient);


                _navigateToPatientFileCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al navegar a la ficha del paciente:\n{ex.Message}", "Error");
            }
        }

        // --- INICIO DE MODIFICACIÓN: Lógica de Paginación ---
        private async Task LoggedSearchAsync(bool resetPage = true)
        {
            if (resetPage)
            {
                CurrentPage = 1;
            }

            string logDetails = string.IsNullOrWhiteSpace(SearchText) ?
                $"Vio la lista de pacientes (Pág. {CurrentPage})" :
                $"Buscó pacientes: '{SearchText}' (Pág. {CurrentPage})";
            _activityLogService.LogAccessAsync(logDetails);

            await SearchPatientsAsync();
        }

        private async Task SearchPatientsAsync()
        {
            Patients.Clear();
            int totalCount = 0;
            IEnumerable<Patient>? patientsFromDb;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                // Llamamos a los nuevos métodos del repositorio
                totalCount = await _patientRepository.GetCountAsync(SearchText, ShowInactivePatients);
                patientsFromDb = await _patientRepository.SearchByNameOrDniAsync(SearchText, ShowInactivePatients, CurrentPage, PageSize);
            }
            else
            {
                // Llamamos a los nuevos métodos del repositorio
                totalCount = await _patientRepository.GetCountAsync(ShowInactivePatients);
                patientsFromDb = await _patientRepository.GetAllAsync(ShowInactivePatients, CurrentPage, PageSize);
            }

            if (patientsFromDb != null)
            {
                // El OrderBy ya se hace en el repositorio, no es necesario aquí
                foreach (var patient in patientsFromDb)
                {
                    Patients.Add(patient);
                }
            }

            // Actualizamos los totales de paginación
            TotalPages = (int)Math.Ceiling((double)totalCount / PageSize);
            if (TotalPages == 0) TotalPages = 1; // Siempre hay al menos 1 página
            if (CurrentPage > TotalPages) CurrentPage = TotalPages; // Corregir si la página actual queda fuera de rango

            PageInfo = $"Página {CurrentPage} de {TotalPages}";

            // Notificamos a los botones para que se habiliten/deshabiliten
            NextPageCommand.NotifyCanExecuteChanged();
            PreviousPageCommand.NotifyCanExecuteChanged();
        }

        // --- Métodos para los comandos de paginación ---
        private bool CanGoNext() => CurrentPage < TotalPages;
        private bool CanGoPrevious() => CurrentPage > 1;

        private async Task NextPageAsync()
        {
            if (CanGoNext())
            {
                CurrentPage++;
                await LoggedSearchAsync(false); // false = no resetear a página 1
            }
        }

        private async Task PreviousPageAsync()
        {
            if (CanGoPrevious())
            {
                CurrentPage--;
                await LoggedSearchAsync(false); // false = no resetear a página 1
            }
        }
        // --- FIN DE MODIFICACIÓN: Lógica de Paginación ---


        private void SetNewPatientForm()
        {
            PatientFormModel = new Patient();
            SelectedPatient = null;
            IsFormEnabled = true;
        }

        private void EditPatient()
        {
            if (SelectedPatient == null) return;
            // --- INICIO DE LA MODIFICACIÓN ---
            // Usamos el método DeepCopy que ya existe en el modelo Patient
            PatientFormModel = SelectedPatient.DeepCopy();
            // --- FIN DE LA MODIFICACIÓN ---
            IsFormEnabled = true;
        }

        // En: TuClinicaUI/ViewModels/PatientsViewModel.cs
        // (Sustituye este método en la clase PatientsViewModel)

        // En: TuClinicaUI/ViewModels/PatientsViewModel.cs

        private async Task SavePatientAsync()
        {
            // 1. Limpieza y formato de datos
            PatientFormModel.Name = PatientFormModel.Name.ToTitleCase();
            PatientFormModel.Surname = PatientFormModel.Surname.ToTitleCase();
            PatientFormModel.DocumentNumber = PatientFormModel.DocumentNumber?.ToUpper().Trim() ?? string.Empty;
            PatientFormModel.Email = PatientFormModel.Email?.ToLower().Trim() ?? string.Empty;

            // 2. Validación de Documento
            if (string.IsNullOrWhiteSpace(PatientFormModel.DocumentNumber))
            {
                _dialogService.ShowMessage("El número de documento no puede estar vacío.", "Dato Requerido");
                return;
            }
            if (!_validationService.IsValidDocument(PatientFormModel.DocumentNumber, PatientFormModel.DocumentType))
            {
                _dialogService.ShowMessage("El número de documento introducido no tiene un formato válido para el tipo seleccionado.", "Documento Inválido");
                return;
            }

            try
            {
                // --- INICIO DE LA MODIFICACIÓN (Lógica de Duplicados Robusta) ---
                if (PatientFormModel.Id == 0) // Es un paciente NUEVO
                {
                    // 1. Buscar por Documento (búsqueda exacta en principal y vinculados)
                    var existingByDoc = (await _patientRepository.SearchByNameOrDniAsync(PatientFormModel.DocumentNumber, true, 1, 10))
                                        .FirstOrDefault();

                    if (existingByDoc != null)
                    {
                        string estado = existingByDoc.IsActive ? "activo" : "archivado";
                        _dialogService.ShowMessage($"El documento '{PatientFormModel.DocumentNumber}' ya existe y pertenece al paciente {estado}: {existingByDoc.PatientDisplayInfo}.", "Documento Duplicado");
                        return;
                    }

                    // 2. Buscar por Teléfono (si se proporcionó)
                    if (!string.IsNullOrWhiteSpace(PatientFormModel.Phone))
                    {
                        var existingByPhone = (await _patientRepository.FindAsync(p => p.Phone == PatientFormModel.Phone && p.IsActive)).FirstOrDefault();
                        if (existingByPhone != null)
                        {
                            var resultPhone = _dialogService.ShowConfirmation(
                                $"ADVERTENCIA: El número de teléfono '{PatientFormModel.Phone}' ya está registrado a nombre de:\n\n" +
                                $"{existingByPhone.PatientDisplayInfo}\n\n" +
                                "¿Está seguro de que este es un paciente nuevo y no un duplicado?",
                                "Posible Duplicado (Teléfono)");

                            if (resultPhone == CoreDialogResult.No)
                            {
                                return; // El usuario cancela la creación
                            }
                        }
                    }

                    // 3. Buscar por Nombre y Apellidos (búsqueda exacta)
                    var existingByName = (await _patientRepository.FindAsync(p =>
                                            p.Name.ToLower() == PatientFormModel.Name.ToLower() &&
                                            p.Surname.ToLower() == PatientFormModel.Surname.ToLower() &&
                                            p.IsActive)).FirstOrDefault();

                    if (existingByName != null)
                    {
                        var resultName = _dialogService.ShowConfirmation(
                               $"ADVERTENCIA: Ya existe un paciente activo con el nombre '{PatientFormModel.Name} {PatientFormModel.Surname}'.\n\n" +
                               $"Documento: {existingByName.DocumentNumber}\n" +
                               $"Teléfono: {existingByName.Phone}\n\n" +
                               "¿Está seguro de que este es un paciente nuevo?",
                               "Posible Duplicado (Nombre)");

                        if (resultName == CoreDialogResult.No)
                        {
                            return; // El usuario cancela la creación
                        }
                    }

                    // 4. Lógica de Reactivación (Si el DNI se encuentra en un paciente archivado)
                    var archivedPatient = (await _patientRepository.FindAsync(p => p.DocumentNumber.ToUpper() == PatientFormModel.DocumentNumber.ToUpper() && !p.IsActive)).FirstOrDefault();
                    if (archivedPatient != null)
                    {
                        var resultReactivate = _dialogService.ShowConfirmation(
                            $"El paciente '{archivedPatient.PatientDisplayInfo}' (Doc: {archivedPatient.DocumentNumber}) ya existe, pero se encuentra archivado.\n\n" +
                            $"¿Desea reactivarlo ahora y actualizar sus datos con los del formulario?",
                            "Paciente Archivado Encontrado");

                        if (resultReactivate == CoreDialogResult.Yes)
                        {
                            var patientToReactivate = await _patientRepository.GetByIdAsync(archivedPatient.Id);
                            if (patientToReactivate != null)
                            {
                                patientToReactivate.CopyFrom(PatientFormModel);
                                patientToReactivate.IsActive = true;
                                _patientRepository.Update(patientToReactivate);
                                _dialogService.ShowMessage("Paciente reactivado y actualizado con éxito.", "Éxito");
                            }
                        }
                        else
                        {
                            return; // El usuario no quiso reactivar
                        }
                    }
                    else
                    {
                        // 5. Si pasa todas las comprobaciones, es un paciente nuevo de verdad
                        await _patientRepository.AddAsync(PatientFormModel);
                        _dialogService.ShowMessage("Paciente creado con éxito.", "Éxito");
                    }
                }
                else // Está EDITANDO un paciente existente (Esta lógica es para la Ficha de Paciente, no para este ViewModel)
                {
                    // Esta lógica se maneja en PatientFileViewModel. La dejamos aquí
                    // por si acaso, pero la lógica principal de edición está en el otro VM.
                    var existingPatient = await _patientRepository.GetByIdAsync(PatientFormModel.Id);
                    if (existingPatient != null)
                    {
                        // Comprobar si ha cambiado el Documento y si el NUEVO Documento ya existe en OTRO paciente
                        if (!string.Equals(existingPatient.DocumentNumber, PatientFormModel.DocumentNumber, StringComparison.OrdinalIgnoreCase))
                        {
                            var duplicateCheck = (await _patientRepository.SearchByNameOrDniAsync(PatientFormModel.DocumentNumber, true, 1, 10))
                                                 .FirstOrDefault(p => p.Id != PatientFormModel.Id);
                            if (duplicateCheck != null)
                            {
                                _dialogService.ShowMessage($"El Documento '{PatientFormModel.DocumentNumber}' ya está asignado a otro paciente.", "Documento Duplicado");
                                return;
                            }
                        }

                        existingPatient.CopyFrom(PatientFormModel);
                        _patientRepository.Update(existingPatient);
                        _dialogService.ShowMessage("Paciente actualizado con éxito.", "Éxito");
                    }
                    else
                    {
                        _dialogService.ShowMessage("Error: No se encontró el paciente que intentaba editar.", "Error");
                        return;
                    }
                }
                // --- FIN DE LA MODIFICACIÓN ---

                // 3. Guardar cambios y refrescar la UI
                await _patientRepository.SaveChangesAsync();
                await LoggedSearchAsync(false); // No resetear la página, solo refrescarla
                PatientFormModel = new Patient();
                IsFormEnabled = false;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al guardar el paciente:\n{ex.Message}", "Error Base de Datos");
            }
        }
        // --- INICIO DE MODIFICACIÓN: Lógica de "Smart Delete" ---
        private async Task DeletePatientAsync()
        {
            if (SelectedPatient == null) return;

            // 1. Obtener el saldo ANTES de preguntar
            decimal currentBalance = await GetPatientBalanceAsync(SelectedPatient.Id);
            string balanceMessage;

            if (currentBalance > 0)
            {
                balanceMessage = $"¡ATENCIÓN: Este paciente tiene un saldo DEUDOR de {currentBalance:C}!";
            }
            else if (currentBalance < 0)
            {
                balanceMessage = $"Este paciente tiene un saldo A FAVOR de {currentBalance:C}.";
            }
            else
            {
                balanceMessage = "Este paciente tiene las cuentas saldadas (0,00 €).";
            }

            // 2. Comprobar historial
            bool hasHistory = await _patientRepository.HasHistoryAsync(SelectedPatient.Id);
            CoreDialogResult result;
            bool isPermanentDelete = false;

            if (hasHistory)
            {
                // 3.A. TIENE HISTORIAL -> Solo puede archivar
                isPermanentDelete = false;
                result = _dialogService.ShowConfirmation(
                    $"{balanceMessage}\n\n" +
                    $"Este paciente tiene historial y NO PUEDE SER ELIMINADO.\n\n" +
                    $"¿Desea ARCHIVARLO en su lugar?",
                    "Confirmar Archivación");
            }
            else
            {
                // 3.B. NO TIENE HISTORIAL -> Puede eliminar permanentemente
                isPermanentDelete = true;
                result = _dialogService.ShowConfirmation(
                    $"{balanceMessage}\n\n" +
                    $"Este paciente NO tiene historial y será ELIMINADO PERMANENTEMENTE.\n\n" +
                    $"¿Está seguro de continuar?",
                    "Confirmar Eliminación Permanente");
            }

            // 4. Salir si el usuario presiona "No"
            if (result == CoreDialogResult.No) return;

            // 5. Ejecutar la acción
            try
            {
                var p = await _patientRepository.GetByIdAsync(SelectedPatient.Id);
                if (p == null) return;

                if (isPermanentDelete)
                {
                    _patientRepository.Remove(p);
                    await _patientRepository.SaveChangesAsync();
                    _dialogService.ShowMessage("Paciente eliminado permanentemente.", "Eliminado");
                }
                else // Archivar
                {
                    p.IsActive = false;
                    _patientRepository.Update(p);
                    await _patientRepository.SaveChangesAsync();
                    _dialogService.ShowMessage("Paciente archivado.", "Archivado");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al procesar la solicitud:\n{ex.Message}", "Error Base de Datos");
            }

            // 6. Refrescar
            await LoggedSearchAsync(false); // Refrescar página actual
            PatientFormModel = new Patient();
            IsFormEnabled = false;
            SelectedPatient = null;
        }
      

        private async Task ReactivatePatientAsync()
        {
            if (SelectedPatient == null) return;

            // --- INICIO DE LA MODIFICACIÓN ---
            // 1. Obtener la entidad completa para revisar sus notas ANTES de preguntar
            Patient? patientToReactivate;
            try
            {
                // Usamos GetByIdAsync para obtener la entidad completa desde la BD
                patientToReactivate = await _patientRepository.GetByIdAsync(SelectedPatient.Id);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al cargar los datos del paciente: {ex.Message}", "Error BD");
                return;
            }

            if (patientToReactivate == null)
            {
                _dialogService.ShowMessage("No se encontró el paciente seleccionado.", "Error");
                await LoggedSearchAsync(false); // Refrescar
                return;
            }

            // 2. Comprobar si es un paciente fusionado (nuestra "bandera" en las notas)
            // Comprobamos si las Notas no son nulas y si contienen nuestra bandera
            if (patientToReactivate.Notes != null && patientToReactivate.Notes.Contains("[FUSIONADO"))
            {
                _dialogService.ShowMessage(
                    "Este paciente no puede ser reactivado.\n\n" +
                    $"Motivo: Sus datos fueron fusionados con otra ficha de paciente. Reactivarlo crearía duplicados.\n\n" +
                    $"{patientToReactivate.Notes}",
                    "Reactivación Bloqueada");
                return; // Salir de la operación
            }
            // --- FIN DE LA MODIFICACIÓN ---

            // 3. Si no está fusionado, proceder con la confirmación normal
            var result = _dialogService.ShowConfirmation($"¿Está seguro de reactivar al paciente '{patientToReactivate.Name} {patientToReactivate.Surname}'?\n\nVolverá a aparecer en la lista principal.", "Confirmar Reactivación");
            if (result == CoreDialogResult.No) return;

            try
            {
                // 4. Actualizar el paciente (ya lo tenemos)
                patientToReactivate.IsActive = true;
                _patientRepository.Update(patientToReactivate);
                await _patientRepository.SaveChangesAsync();
                _dialogService.ShowMessage("Paciente reactivado con éxito.", "Éxito");

                await LoggedSearchAsync(false); // Refrescar página actual
                SelectedPatient = null;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al reactivar el paciente:\n{ex.Message}", "Error Base de Datos");
            }
        }

        // --- MODIFICACIÓN: Este método se ha eliminado ---
        // private async Task DeletePatientPermanentlyAsync() { ... }


        private void CreatePrescription()
        {
            if (SelectedPatient == null)
            {
                _dialogService.ShowMessage("Seleccione un paciente primero.", "Paciente no seleccionado");
                return;
            }
            _dialogService.ShowMessage($"Funcionalidad pendiente: Abrir diálogo de receta para {SelectedPatient.Name} (ID: {SelectedPatient.Id}).", "Pendiente");
        }

        // --- MÉTODOS PARCIALES (Notificación de cambios) ---

        partial void OnSelectedPatientChanged(Patient? value)
        {
            IsFormEnabled = false;
            EditPatientCommand.NotifyCanExecuteChanged();
            CreatePrescriptionCommand.NotifyCanExecuteChanged();
            ViewPatientDetailsCommand.NotifyCanExecuteChanged();

            OnPropertyChanged(nameof(IsPatientSelectedAndActive));
            OnPropertyChanged(nameof(IsPatientSelectedAndInactive));
            DeletePatientAsyncCommand.NotifyCanExecuteChanged();
            ReactivatePatientAsyncCommand.NotifyCanExecuteChanged();
            // --- MODIFICACIÓN: Línea eliminada ---
            // DeletePatientPermanentlyAsyncCommand.NotifyCanExecuteChanged();
        }

        // --- INICIO DE MODIFICACIÓN: Paginación ---
        partial void OnSearchTextChanged(string value)
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            _ = DebouncedSearchAsync(_searchCts.Token, true); // true = resetear a página 1
        }

        partial void OnShowInactivePatientsChanged(bool value)
        {
            _ = LoggedSearchAsync(true); // true = resetear a página 1
        }

        private async Task DebouncedSearchAsync(CancellationToken token, bool resetPage)
        {
            try
            {
                await Task.Delay(300, token);
                await LoggedSearchAsync(resetPage);
            }
            catch (TaskCanceledException)
            {
                // No hacer nada
            }
        }
        // --- FIN DE MODIFICACIÓN: Paginación ---


        private async Task<decimal> GetPatientBalanceAsync(int patientId)
        {
            try
            {
                // Usamos el scope factory para obtener los repositorios de facturación
                // de forma segura, ya que PatientsViewModel no los tiene inyectados.
                using (var scope = _scopeFactory.CreateScope())
                {
                    var entryRepo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();
                    var paymentRepo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

                    var entriesTask = entryRepo.GetHistoryForPatientAsync(patientId);
                    var paymentsTask = paymentRepo.GetPaymentsForPatientAsync(patientId);

                    await Task.WhenAll(entriesTask, paymentsTask);

                    decimal totalCharged = entriesTask.Result?.Sum(e => e.TotalCost) ?? 0;
                    decimal totalPaid = paymentsTask.Result?.Sum(p => p.Amount) ?? 0;

                    return totalCharged - totalPaid;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"No se pudo calcular el saldo del paciente: {ex.Message}", "Error de Saldo");
                // Devolvemos 0 para no bloquear la operación, aunque mostraremos un error.
                return 0;
            }
        }
    }
}