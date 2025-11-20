using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Models;
using System.Windows;
using System.ComponentModel;
using System.Windows.Input;
using TuClinica.Core.Interfaces.Services;
using CoreDialogResult = TuClinica.Core.Interfaces.Services.DialogResult;
using System.Linq;
using TuClinica.Core.Interfaces;
using System.Collections.Generic;

namespace TuClinica.UI.ViewModels
{
    public partial class TreatmentsViewModel : BaseViewModel
    {
        private readonly ITreatmentRepository _treatmentRepository;
        private readonly IDialogService _dialogService;
        private readonly IRepository<TreatmentPackItem> _packItemRepository;

        [ObservableProperty]
        private ObservableCollection<Treatment> _treatments = new ObservableCollection<Treatment>();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsTreatmentSelected))]
        [NotifyCanExecuteChangedFor(nameof(EditTreatmentCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteTreatmentCommand))]
        private Treatment? _selectedTreatment;

        [ObservableProperty]
        private Treatment _treatmentFormModel = new Treatment { IsActive = true };

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveTreatmentCommand))]
        private bool _isFormEnabled = false;

        public bool IsTreatmentSelected => SelectedTreatment != null;

        [ObservableProperty]
        private ObservableCollection<Treatment> _availableComponents = new ObservableCollection<Treatment>();

        [ObservableProperty]
        private ObservableCollection<TreatmentPackItemViewModel> _currentPackItems = new ObservableCollection<TreatmentPackItemViewModel>();

        [ObservableProperty]
        private Treatment? _selectedComponentToAdd;

        [ObservableProperty]
        private int _componentQuantity = 1;

        public TreatmentsViewModel(
            ITreatmentRepository treatmentRepository,
            IDialogService dialogService,
            IRepository<TreatmentPackItem> packItemRepository)
        {
            _treatmentRepository = treatmentRepository;
            _dialogService = dialogService;
            _packItemRepository = packItemRepository;

            _ = LoadTreatmentsAsync();
        }

        [RelayCommand]
        private void AddPackItem()
        {
            if (SelectedComponentToAdd == null)
            {
                _dialogService.ShowMessage("Por favor, seleccione un tratamiento para añadir como componente.", "Error");
                return;
            }

            if (ComponentQuantity <= 0)
            {
                _dialogService.ShowMessage("La cantidad debe ser 1 o mayor.", "Error");
                return;
            }

            if (IsTreatmentSelected && SelectedTreatment!.Id == SelectedComponentToAdd.Id)
            {
                _dialogService.ShowMessage("No puede añadir un pack como componente de sí mismo.", "Error");
                return;
            }

            if (CurrentPackItems.Any(p => p.ChildTreatmentId == SelectedComponentToAdd.Id))
            {
                _dialogService.ShowMessage("Este componente ya existe en el pack. Puede editar la cantidad.", "Componente Duplicado");
                return;
            }

            var newPackItem = new TreatmentPackItem
            {
                ChildTreatmentId = SelectedComponentToAdd.Id,
                ChildTreatment = SelectedComponentToAdd,
                Quantity = ComponentQuantity
            };

            CurrentPackItems.Add(new TreatmentPackItemViewModel(newPackItem));
            SelectedComponentToAdd = null;
            ComponentQuantity = 1;
        }

        [RelayCommand]
        private void RemovePackItem(TreatmentPackItemViewModel? itemToRemove)
        {
            if (itemToRemove != null)
            {
                CurrentPackItems.Remove(itemToRemove);
            }
        }

        [RelayCommand]
        private async Task LoadTreatmentsAsync()
        {
            var treatmentsFromDb = await _treatmentRepository.GetAllAsync();

            Treatments.Clear();
            AvailableComponents.Clear();

            if (treatmentsFromDb != null)
            {
                var orderedTreatments = treatmentsFromDb.OrderBy(t => t.Name).ToList();

                foreach (var treatment in orderedTreatments)
                {
                    Treatments.Add(treatment);
                    AvailableComponents.Add(treatment);
                }
            }
        }

        [RelayCommand]
        private void SetNewTreatmentForm()
        {
            TreatmentFormModel = new Treatment { IsActive = true };
            IsFormEnabled = true;
            SelectedTreatment = null;
        }

        [RelayCommand(CanExecute = nameof(IsTreatmentSelected))]
        private void EditTreatment()
        {
            if (SelectedTreatment == null) return;
            TreatmentFormModel = new Treatment
            {
                Id = SelectedTreatment.Id,
                Name = SelectedTreatment.Name,
                Description = SelectedTreatment.Description,
                DefaultPrice = SelectedTreatment.DefaultPrice,
                IsActive = SelectedTreatment.IsActive
            };
            IsFormEnabled = true;
        }

        partial void OnSelectedTreatmentChanged(Treatment? value)
        {
            CurrentPackItems.Clear();
            if (value != null)
            {
                if (value.PackItems != null)
                {
                    foreach (var packItem in value.PackItems.OrderBy(p => p.ChildTreatment?.Name))
                    {
                        CurrentPackItems.Add(new TreatmentPackItemViewModel(packItem));
                    }
                }
            }
        }

        [RelayCommand(CanExecute = nameof(IsFormEnabled))]
        private async Task SaveTreatmentAsync()
        {
            if (string.IsNullOrWhiteSpace(TreatmentFormModel.Name))
            {
                _dialogService.ShowMessage("El nombre del tratamiento no puede estar vacío.", "Error");
                return;
            }
            if (TreatmentFormModel.DefaultPrice < 0)
            {
                _dialogService.ShowMessage("El precio no puede ser negativo.", "Error");
                return;
            }

            bool isNew = (TreatmentFormModel.Id == 0);
            Treatment? savedTreatment;

            if (isNew)
            {
                // Nuevo: Creamos y añadimos
                savedTreatment = TreatmentFormModel;
                await _treatmentRepository.AddAsync(savedTreatment);
            }
            else
            {
                // Edición: Usamos el método que trae la entidad RASTREADA (Tracked)
                // Esto evita el error "Instance already tracked"
                savedTreatment = await _treatmentRepository.GetByIdWithPackItemsAsync(TreatmentFormModel.Id);

                if (savedTreatment == null)
                {
                    _dialogService.ShowMessage("Error: No se encontró el tratamiento a editar.", "Error");
                    return;
                }

                // Actualizamos las propiedades de la entidad rastreada
                savedTreatment.Name = TreatmentFormModel.Name;
                savedTreatment.Description = TreatmentFormModel.Description;
                savedTreatment.DefaultPrice = TreatmentFormModel.DefaultPrice;
                savedTreatment.IsActive = TreatmentFormModel.IsActive;

                // Nota: No es necesario llamar a _treatmentRepository.Update() si la entidad ya está rastreada y modificada,
                // pero llamarlo no hace daño si es la misma instancia.
                _treatmentRepository.Update(savedTreatment);
            }

            await _treatmentRepository.SaveChangesAsync(); // Guardamos cabecera para tener ID

            // Guardar componentes del pack
            if (CurrentPackItems.Any() || !isNew)
            {
                List<TreatmentPackItem> oldItems = new List<TreatmentPackItem>();
                if (!isNew && savedTreatment != null && savedTreatment.PackItems != null)
                {
                    oldItems = savedTreatment.PackItems.ToList();
                }

                var itemsInUi = CurrentPackItems.Select(vm => vm.Model).ToList();

                // Eliminar
                var itemsToDelete = oldItems.Where(dbItem => !itemsInUi.Any(uiItem => uiItem.ChildTreatmentId == dbItem.ChildTreatmentId)).ToList();
                if (itemsToDelete.Any())
                {
                    _packItemRepository.RemoveRange(itemsToDelete);
                }

                // Añadir/Actualizar
                foreach (var uiItemVm in CurrentPackItems)
                {
                    var dbItem = oldItems.FirstOrDefault(oi => oi.ChildTreatmentId == uiItemVm.ChildTreatmentId);
                    if (dbItem == null)
                    {
                        // Nuevo item
                        uiItemVm.Model.ParentTreatmentId = savedTreatment!.Id;
                        uiItemVm.Model.ChildTreatment = null; // Evitar reinsertar hijo
                        await _packItemRepository.AddAsync(uiItemVm.Model);
                    }
                    else
                    {
                        // Existente
                        dbItem.Quantity = uiItemVm.Quantity;
                        _packItemRepository.Update(dbItem);
                    }
                }
            }

            await _packItemRepository.SaveChangesAsync();
            await LoadTreatmentsAsync();

            TreatmentFormModel = new Treatment { IsActive = true };
            IsFormEnabled = false;
            SelectedTreatment = null;
        }

        [RelayCommand(CanExecute = nameof(IsTreatmentSelected))]
        private async Task DeleteTreatmentAsync()
        {
            if (SelectedTreatment == null) return;

            if (SelectedTreatment.PackItems != null && SelectedTreatment.PackItems.Any())
            {
                _dialogService.ShowMessage($"No se puede eliminar '{SelectedTreatment.Name}' porque es un Pack.\nPrimero debe eliminar todos sus componentes.", "Error al Eliminar");
                return;
            }

            var result = _dialogService.ShowConfirmation($"¿Eliminar PERMANENTEMENTE el tratamiento '{SelectedTreatment.Name}'? Esta acción no se puede deshacer.",
                                             "Confirmar Eliminación Permanente");

            if (result == CoreDialogResult.No) return;

            try
            {
                // Usamos GetByIdAsync para obtener la entidad rastreada antes de borrar
                var treatmentToDelete = await _treatmentRepository.GetByIdAsync(SelectedTreatment.Id);
                if (treatmentToDelete != null)
                {
                    _treatmentRepository.Remove(treatmentToDelete);
                    await _treatmentRepository.SaveChangesAsync();
                    _dialogService.ShowMessage("Tratamiento eliminado.", "Eliminado");
                }
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                _dialogService.ShowMessage($"No se pudo eliminar el tratamiento.\n\nCAUSA PROBABLE: Está siendo utilizado como componente en uno o más 'Packs' o en un historial clínico.\n\nError: {dbEx.InnerException?.Message ?? dbEx.Message}", "Error de Base de Datos");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al eliminar: {ex.Message}", "Error");
            }

            await LoadTreatmentsAsync();
            TreatmentFormModel = new Treatment { IsActive = true };
            IsFormEnabled = false;
            SelectedTreatment = null;
        }
    }
}