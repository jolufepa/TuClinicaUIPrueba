using CommunityToolkit.Mvvm.ComponentModel;
using TuClinica.Core.Models;

namespace TuClinica.UI.ViewModels
{
    /// <summary>
    /// ViewModel wrapper para un TreatmentPackItem para mostrarlo en la UI
    /// de gestión de Tratamientos.
    /// </summary>
    public partial class TreatmentPackItemViewModel : ObservableObject
    {
        // Almacenamos el modelo original
        public TreatmentPackItem Model { get; }

        [ObservableProperty]
        private string _childTreatmentName;

        [ObservableProperty]
        private int _quantity;

        // Referencia al Treatment "hijo" real
        public Treatment ChildTreatment { get; }

        public int ChildTreatmentId => ChildTreatment.Id;

        public TreatmentPackItemViewModel(TreatmentPackItem model)
        {
            Model = model;
            ChildTreatment = model.ChildTreatment!; // Asumimos que no es nulo
            _childTreatmentName = ChildTreatment.Name;
            _quantity = model.Quantity;

            // Escuchar cambios en la cantidad si se edita en el grid
            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Quantity))
                {
                    Model.Quantity = this.Quantity;
                }
            };
        }
    }
}