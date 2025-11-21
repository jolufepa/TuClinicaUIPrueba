using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.IO; // Para Path
using System.Linq;
using System.Threading.Tasks;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces; // Para IRepository
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.DataAccess; // Para AppDbContext si fuera necesario acceder directo, pero usamos IRepository
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;

namespace TuClinica.UI.ViewModels
{
    public partial class PatientDocumentsViewModel : BaseViewModel
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDialogService _dialogService;
        private readonly IAuthService _authService;
        private readonly IFileDialogService _fileDialogService; // <-- Necesitamos esto para abrir archivos
        private readonly IFileStorageService _fileStorageService; // <-- NUEVO

        [ObservableProperty] private Patient? _currentPatient;
        [ObservableProperty] private bool _canManageDocuments = false;

        // --- Colección Documentos Vinculados (Existente) ---
        [ObservableProperty] private ObservableCollection<LinkedDocument> _linkedDocuments = new();
        [ObservableProperty] private LinkedDocument? _selectedLinkedDocument;

        // --- Colección Archivos Adjuntos (NUEVO) ---
        [ObservableProperty] private ObservableCollection<PatientFile> _patientFiles = new();
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(OpenFileCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteFileCommand))]
        private PatientFile? _selectedFile;

        // Comandos Existentes
        public IAsyncRelayCommand AddLinkedDocumentCommand { get; }
        public IAsyncRelayCommand DeleteLinkedDocumentCommand { get; }

        // Comandos NUEVOS
        public IAsyncRelayCommand AddFileCommand { get; }
        public IAsyncRelayCommand OpenFileCommand { get; }
        public IAsyncRelayCommand DeleteFileCommand { get; }

        public PatientDocumentsViewModel(
            IServiceScopeFactory scopeFactory,
            IDialogService dialogService,
            IAuthService authService,
            IFileDialogService fileDialogService, // Inyectado
            IFileStorageService fileStorageService) // Inyectado
        {
            _scopeFactory = scopeFactory;
            _dialogService = dialogService;
            _authService = authService;
            _fileDialogService = fileDialogService;
            _fileStorageService = fileStorageService;

            AddLinkedDocumentCommand = new AsyncRelayCommand(AddLinkedDocumentAsync);
            DeleteLinkedDocumentCommand = new AsyncRelayCommand(DeleteLinkedDocumentAsync, () => SelectedLinkedDocument != null);

            // Inicializar nuevos comandos
            AddFileCommand = new AsyncRelayCommand(AddFileAsync);
            OpenFileCommand = new AsyncRelayCommand(OpenFileAsync, () => SelectedFile != null);
            DeleteFileCommand = new AsyncRelayCommand(DeleteFileAsync, () => SelectedFile != null);

            CheckPermissions();
        }

        private void CheckPermissions()
        {
            var user = _authService.CurrentUser;
            if (user != null && (user.Role == UserRole.Administrador || user.Role == UserRole.Doctor))
            {
                CanManageDocuments = true;
            }
        }

        public void Load(Patient patient)
        {
            CurrentPatient = patient;
            LoadLinkedDocuments(patient);
            _ = LoadPatientFilesAsync(patient.Id); // Carga asíncrona de archivos
        }

        private void LoadLinkedDocuments(Patient patient)
        {
            LinkedDocuments.Clear();
            if (patient.LinkedDocuments != null)
            {
                foreach (var doc in patient.LinkedDocuments.OrderBy(d => d.DocumentType))
                    LinkedDocuments.Add(doc);
            }
        }

        private async Task LoadPatientFilesAsync(int patientId)
        {
            try
            {
                PatientFiles.Clear();
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IRepository<PatientFile>>();
                    var files = await repo.FindAsync(f => f.PatientId == patientId);

                    foreach (var f in files.OrderByDescending(x => x.UploadDate))
                        PatientFiles.Add(f);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Error cargando archivos: " + ex.Message, "Error");
            }
        }

        // --- Lógica para Archivos Físicos (NUEVO) ---

        private async Task AddFileAsync()
        {
            if (CurrentPatient == null) return;

            // 1. Seleccionar el archivo físico
            var (ok, path) = _fileDialogService.ShowOpenDialog("Documentos|*.pdf;*.jpg;*.jpeg;*.png;*.docx", "Seleccionar Documento");
            if (!ok) return;

            // Obtener solo el nombre del archivo (sin ruta) para sugerírselo al usuario
            string originalFileName = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);

            // 2. NUEVO: Mostrar diálogo para elegir Categoría y editar Nombre
            var (confirmed, customName, category) = _dialogService.ShowDocumentDetailsDialog(originalFileName);

            if (!confirmed) return; // El usuario canceló en la ventana de detalles

            try
            {
                // Construir el nombre final con la extensión original
                string finalFileNameDisplay = customName + extension;

                // 3. Guardar físico (genera un GUID interno, no nos importa el nombre aquí)
                string relPath = await _fileStorageService.SaveFileAsync(path, CurrentPatient.Id);

                // 4. Guardar en BD con los datos elegidos por el usuario
                var newFile = new PatientFile
                {
                    PatientId = CurrentPatient.Id,
                    FileName = finalFileNameDisplay, // Nombre bonito elegido por usuario
                    RelativePath = relPath,          // Ruta técnica
                    Category = category,             // Categoría elegida
                    UploadDate = DateTime.Now
                };

                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IRepository<PatientFile>>();
                    await repo.AddAsync(newFile);
                    await repo.SaveChangesAsync();
                }

                // Insertar al principio para ver el más reciente arriba
                PatientFiles.Insert(0, newFile);

                _dialogService.ShowMessage("Documento adjuntado correctamente.", "Éxito");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al adjuntar: {ex.Message}", "Error");
            }
        }

        private async Task OpenFileAsync()
        {
            if (SelectedFile == null) return;
            try
            {
                _fileStorageService.OpenFile(SelectedFile.RelativePath);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage(ex.Message, "Error al abrir");
            }
        }

        private async Task DeleteFileAsync()
        {
            if (SelectedFile == null) return;
            if (_dialogService.ShowConfirmation($"¿Borrar archivo '{SelectedFile.FileName}' permanentemente?", "Confirmar") == CoreDialogResult.No) return;

            try
            {
                // 1. Borrar físico
                _fileStorageService.DeleteFile(SelectedFile.RelativePath);

                // 2. Borrar BD
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IRepository<PatientFile>>();
                    // Necesitamos la instancia trackeada
                    var dbFile = await repo.GetByIdAsync(SelectedFile.Id);
                    if (dbFile != null)
                    {
                        repo.Remove(dbFile);
                        await repo.SaveChangesAsync();
                    }
                }

                PatientFiles.Remove(SelectedFile);
                SelectedFile = null;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al borrar: {ex.Message}", "Error");
            }
        }

        // ... (Mantener métodos AddLinkedDocumentAsync y DeleteLinkedDocumentAsync existentes) ...
        // He omitido el código de LinkedDocuments porque ya lo tienes y no cambia, solo añade lo nuevo.
        private async Task AddLinkedDocumentAsync()
        {
            if (CurrentPatient == null || !CanManageDocuments) return;
            var (ok, docType, docNum, notes) = _dialogService.ShowLinkedDocumentDialog();
            if (!ok) return;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IRepository<LinkedDocument>>();
                    var newDoc = new LinkedDocument { PatientId = CurrentPatient.Id, DocumentType = docType, DocumentNumber = docNum, Notes = notes };
                    await repo.AddAsync(newDoc);
                    await repo.SaveChangesAsync();
                    LinkedDocuments.Add(newDoc);
                }
            }
            catch (Exception ex) { _dialogService.ShowMessage($"Error: {ex.Message}", "Error"); }
        }

        private async Task DeleteLinkedDocumentAsync()
        {
            if (SelectedLinkedDocument == null) return;
            if (_dialogService.ShowConfirmation("¿Eliminar documento?", "Confirmar") == CoreDialogResult.No) return;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IRepository<LinkedDocument>>();
                    var doc = await repo.GetByIdAsync(SelectedLinkedDocument.Id);
                    if (doc != null) { repo.Remove(doc); await repo.SaveChangesAsync(); }
                }
                LinkedDocuments.Remove(SelectedLinkedDocument);
                SelectedLinkedDocument = null;
            }
            catch (Exception ex) { _dialogService.ShowMessage(ex.Message, "Error"); }
        }
    }
}