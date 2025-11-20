using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using TuClinica.DataAccess;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;

namespace TuClinica.UI.ViewModels
{
    public partial class PatientDocumentsViewModel : BaseViewModel
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDialogService _dialogService;
        private readonly IAuthService _authService;

        [ObservableProperty]
        private Patient? _currentPatient;

        [ObservableProperty]
        private ObservableCollection<LinkedDocument> _linkedDocuments = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeleteLinkedDocumentCommand))]
        private LinkedDocument? _selectedLinkedDocument;

        [ObservableProperty]
        private bool _canManageDocuments = false;

        public IAsyncRelayCommand AddLinkedDocumentCommand { get; }
        public IAsyncRelayCommand DeleteLinkedDocumentCommand { get; }

        public PatientDocumentsViewModel(
            IServiceScopeFactory scopeFactory,
            IDialogService dialogService,
            IAuthService authService)
        {
            _scopeFactory = scopeFactory;
            _dialogService = dialogService;
            _authService = authService;

            AddLinkedDocumentCommand = new AsyncRelayCommand(AddLinkedDocumentAsync);
            DeleteLinkedDocumentCommand = new AsyncRelayCommand(DeleteLinkedDocumentAsync, CanDeleteDocument);

            // Inicializar permisos
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
            LinkedDocuments.Clear();

            if (patient.LinkedDocuments != null)
            {
                foreach (var doc in patient.LinkedDocuments.OrderBy(d => d.DocumentType).ThenBy(d => d.DocumentNumber))
                {
                    LinkedDocuments.Add(doc);
                }
            }
        }

        private bool CanDeleteDocument() => SelectedLinkedDocument != null;

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
                    var newDoc = new LinkedDocument
                    {
                        PatientId = CurrentPatient.Id,
                        DocumentType = docType,
                        DocumentNumber = docNum,
                        Notes = notes
                    };

                    await repo.AddAsync(newDoc);
                    await repo.SaveChangesAsync();

                    // Actualizar UI
                    LinkedDocuments.Add(newDoc);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error: {ex.Message}", "Error");
            }
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
                    // Obtener entidad rastreada para borrar
                    var doc = await repo.GetByIdAsync(SelectedLinkedDocument.Id);
                    if (doc != null)
                    {
                        repo.Remove(doc);
                        await repo.SaveChangesAsync();
                    }
                }
                LinkedDocuments.Remove(SelectedLinkedDocument);
                SelectedLinkedDocument = null;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage(ex.Message, "Error");
            }
        }
    }
}