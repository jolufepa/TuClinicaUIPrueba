// En: TuClinicaUI/ViewModels/TreatmentsViewModel.cs
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
using System.Linq; // <-- AÑADIR
using TuClinica.Core.Interfaces; // <-- AÑADIR
using System.Collections.Generic; // <-- AÑADIR

namespace TuClinica.UI.ViewModels
{
    public partial class TreatmentsViewModel : BaseViewModel
    {
        private readonly ITreatmentRepository _treatmentRepository;
        private readonly IDialogService _dialogService;
        // --- INICIO DE MODIFICACIÓN ---
        private readonly IRepository<TreatmentPackItem> _packItemRepository;
        // --- FIN DE MODIFICACIÓN ---

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
        [NotifyCanExecuteChangedFor(nameof(SaveTreatmentCommand))] // Habilitar/Deshabilitar Guardar
        private bool _isFormEnabled = false;

        public bool IsTreatmentSelected => SelectedTreatment != null;

        // --- INICIO DE MODIFICACIÓN (Nuevas propiedades para Packs) ---

        /// <summary>
        /// Lista de todos los tratamientos para el ComboBox "Añadir Componente".
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Treatment> _availableComponents = new ObservableCollection<Treatment>();

        /// <summary>
        /// Lista de componentes del pack *actualmente seleccionado*.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<TreatmentPackItemViewModel> _currentPackItems = new ObservableCollection<TreatmentPackItemViewModel>();

        /// <summary>
        /// Componente (Tratamiento) seleccionado en el ComboBox para añadir al pack.
        /// </summary>
        [ObservableProperty]
        private Treatment? _selectedComponentToAdd;

        /// <summary>
        /// Cantidad del componente a añadir.
        /// </summary>
        [ObservableProperty]
        private int _componentQuantity = 1;

        // --- FIN DE MODIFICACIÓN ---

        public TreatmentsViewModel(
            ITreatmentRepository treatmentRepository,
            IDialogService dialogService,
            // --- INICIO DE MODIFICACIÓN ---
            IRepository<TreatmentPackItem> packItemRepository // Inyectar el nuevo repositorio
                                                              // --- FIN DE MODIFICACIÓN ---
            )
        {
            _treatmentRepository = treatmentRepository;
            _dialogService = dialogService;
            // --- INICIO DE MODIFICACIÓN ---
            _packItemRepository = packItemRepository;
            // --- FIN DE MODIFICACIÓN ---

            _ = LoadTreatmentsAsync();
        }

        // --- INICIO DE MODIFICACIÓN (Nuevos Comandos para Packs) ---
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

            // Comprobar si ya existe para evitar duplicados en la UI
            if (CurrentPackItems.Any(p => p.ChildTreatmentId == SelectedComponentToAdd.Id))
            {
                _dialogService.ShowMessage("Este componente ya existe en el pack. Puede editar la cantidad.", "Componente Duplicado");
                return;
            }

            // Crear el modelo de datos
            var newPackItem = new TreatmentPackItem
            {
                // El ID del padre (pack) se asignará al guardar
                ChildTreatmentId = SelectedComponentToAdd.Id,
                ChildTreatment = SelectedComponentToAdd,
                Quantity = ComponentQuantity
            };

            // Añadir el ViewModel a la lista de la UI
            CurrentPackItems.Add(new TreatmentPackItemViewModel(newPackItem));

            // Resetear
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
        // --- FIN DE MODIFICACIÓN ---

        [RelayCommand]
        private async Task LoadTreatmentsAsync()
        {
            // --- INICIO DE MODIFICACIÓN ---
            // Usamos el repositorio que ya incluye los PackItems
            var treatmentsFromDb = await _treatmentRepository.GetAllAsync();

            Treatments.Clear();
            AvailableComponents.Clear(); // Limpiar también la lista de componentes

            if (treatmentsFromDb != null)
            {
                // Ordenar por nombre al llenar la lista principal
                var orderedTreatments = treatmentsFromDb.OrderBy(t => t.Name).ToList();

                foreach (var treatment in orderedTreatments)
                {
                    Treatments.Add(treatment);
                    AvailableComponents.Add(treatment); // Llenar la lista de componentes con todos
                }

                // Opcional: si no quieres que los packs aparezcan en la lista de "componentes"
                // var nonPackTreatments = orderedTreatments.Where(t => t.PackItems == null || !t.PackItems.Any()).ToList();
                // foreach (var treatment in nonPackTreatments)
                // {
                //     AvailableComponents.Add(treatment);
                // }
            }
            // --- FIN DE MODIFICACIÓN ---
        }

        [RelayCommand]
        private void SetNewTreatmentForm()
        {
            TreatmentFormModel = new Treatment { IsActive = true };
            IsFormEnabled = true;
            SelectedTreatment = null; // Esto disparará OnSelectedTreatmentChanged
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
                // No copiamos la colección, OnSelectedTreatmentChanged la cargará
            };
            IsFormEnabled = true;
        }

        // --- MÉTODO OnSelectedTreatmentChanged (AÑADIDO CON 'partial') ---
        // Este método se activa automáticamente cuando la propiedad SelectedTreatment cambia.
        partial void OnSelectedTreatmentChanged(Treatment? value)
        {
            // Limpiar la lista de componentes del pack anterior
            CurrentPackItems.Clear();

            if (value != null)
            {
                // Si el tratamiento seleccionado tiene ítems, los cargamos
                if (value.PackItems != null)
                {
                    foreach (var packItem in value.PackItems.OrderBy(p => p.ChildTreatment?.Name))
                    {
                        // Creamos un ViewModel por cada ítem y lo añadimos a la lista de la UI
                        CurrentPackItems.Add(new TreatmentPackItemViewModel(packItem));
                    }
                }
            }
        }
        // --- FIN DE MODIFICACIÓN ---


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

            // --- INICIO DE MODIFICACIÓN (Guardar Packs) ---
            bool isNew = (TreatmentFormModel.Id == 0);

            // 1. Guardar el tratamiento principal (Pack o Individual)
            Treatment? savedTreatment;
            if (isNew)
            {
                // Es un nuevo tratamiento
                savedTreatment = TreatmentFormModel;
                await _treatmentRepository.AddAsync(savedTreatment);
            }
            else
            {
                // Es una edición
                // IMPORTANTE: NO usar GetByIdAsync. Necesitamos la entidad rastreada.
                // Usamos el repositorio de packs para obtener la entidad con sus hijos.
                savedTreatment = await _treatmentRepository.GetAllAsync()
                                     .ContinueWith(t => t.Result.FirstOrDefault(tr => tr.Id == TreatmentFormModel.Id));

                if (savedTreatment == null)
                {
                    _dialogService.ShowMessage("Error: No se encontró el tratamiento a editar.", "Error");
                    return;
                }

                savedTreatment.Name = TreatmentFormModel.Name;
                savedTreatment.Description = TreatmentFormModel.Description;
                savedTreatment.DefaultPrice = TreatmentFormModel.DefaultPrice;
                savedTreatment.IsActive = TreatmentFormModel.IsActive;
                _treatmentRepository.Update(savedTreatment);
            }
            // Guardamos el tratamiento principal para obtener su ID (si es nuevo)
            await _treatmentRepository.SaveChangesAsync();


            // 2. Guardar los componentes del pack (si los hay)
            if (CurrentPackItems.Any() || !isNew)
            {
                // 2a. Obtener los componentes antiguos (solo en modo Edición)
                List<TreatmentPackItem> oldItems = new List<TreatmentPackItem>();
                if (!isNew && savedTreatment != null)
                {
                    // Usamos la colección ya cargada en la entidad 'savedTreatment'
                    oldItems = savedTreatment.PackItems.ToList();
                }

                // 2b. Sincronizar
                var itemsInUi = CurrentPackItems.Select(vm => vm.Model).ToList();

                // Eliminar los que ya no están en la UI
                var itemsToDelete = oldItems.Where(dbItem => !itemsInUi.Any(uiItem => uiItem.ChildTreatmentId == dbItem.ChildTreatmentId)).ToList();
                if (itemsToDelete.Any())
                {
                    _packItemRepository.RemoveRange(itemsToDelete);
                }

                // Añadir/Actualizar los que están en la UI
                foreach (var uiItemVm in CurrentPackItems)
                {
                    var dbItem = oldItems.FirstOrDefault(oi => oi.ChildTreatmentId == uiItemVm.ChildTreatmentId);
                    if (dbItem == null)
                    {
                        // Es nuevo
                        uiItemVm.Model.ParentTreatmentId = savedTreatment!.Id; // Asignar el ID del Padre
                        // Asegurarnos de que el ChildTreatment no se intente insertar de nuevo
                        uiItemVm.Model.ChildTreatment = null;
                        await _packItemRepository.AddAsync(uiItemVm.Model);
                    }
                    else
                    {
                        // Es existente, actualizar cantidad
                        dbItem.Quantity = uiItemVm.Quantity;
                        _packItemRepository.Update(dbItem);
                    }
                }
            }
            // --- FIN DE MODIFICACIÓN ---

            await _packItemRepository.SaveChangesAsync(); // Guardar los cambios de los packs
            await LoadTreatmentsAsync(); // Recargar todo

            TreatmentFormModel = new Treatment { IsActive = true };
            IsFormEnabled = false;
            SelectedTreatment = null; // Limpiar selección
        }

        [RelayCommand(CanExecute = nameof(IsTreatmentSelected))]
        private async Task DeleteTreatmentAsync()
        {
            if (SelectedTreatment == null) return;

            // --- INICIO DE MODIFICACIÓN ---
            // Comprobación mejorada: AHORA SÍ tenemos la info de los packs cargada
            if (SelectedTreatment.PackItems != null && SelectedTreatment.PackItems.Any())
            {
                _dialogService.ShowMessage($"No se puede eliminar '{SelectedTreatment.Name}' porque es un Pack.\nPrimero debe eliminar todos sus componentes.", "Error al Eliminar");
                return;
            }
            // (La restricción de borrado si es un *componente* de otro pack
            // la manejará la base de datos (OnDelete.Restrict) y
            // mostrará un error en el catch)
            // --- FIN DE MODIFICACIÓN ---

            var result = _dialogService.ShowConfirmation($"¿Eliminar PERMANENTEMENTE el tratamiento '{SelectedTreatment.Name}'? Esta acción no se puede deshacer.",
                                             "Confirmar Eliminación Permanente");

            if (result == CoreDialogResult.No) return;

            try // --- AÑADIDO TRY-CATCH ---
            {
                // No podemos borrar SelectedTreatment porque viene de una query AsNoTracking
                // Tenemos que obtener la entidad rastreada por su ID
                var treatmentToDelete = await _treatmentRepository.GetByIdAsync(SelectedTreatment.Id);
                if (treatmentToDelete != null)
                {
                    _treatmentRepository.Remove(treatmentToDelete);
                    await _treatmentRepository.SaveChangesAsync();
                    _dialogService.ShowMessage("Tratamiento eliminado.", "Eliminado");
                }
            }
            // --- INICIO DE MODIFICACIÓN (Capturar error de FK) ---
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                _dialogService.ShowMessage($"No se pudo eliminar el tratamiento.\n\nCAUSA PROBABLE: Está siendo utilizado como componente en uno o más 'Packs' o en un historial clínico.\n\nError: {dbEx.InnerException?.Message ?? dbEx.Message}", "Error de Base de Datos");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al eliminar: {ex.Message}", "Error");
            }
            // --- FIN DE MODIFICACIÓN ---

            await LoadTreatmentsAsync();
            TreatmentFormModel = new Treatment { IsActive = true };
            IsFormEnabled = false;
            SelectedTreatment = null;
        }
    }
}