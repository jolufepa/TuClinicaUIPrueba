using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel; // Para PropertyChangedEventArgs

namespace TuClinica.Core.Models
{
    // Cambiamos a ObservableValidator si _description tiene [Required]
    public partial class BudgetLineItem : ObservableValidator
    {
        [Key]
        public int Id { get; set; }
        public int BudgetId { get; set; }
        [ForeignKey("BudgetId")]
        public Budget Budget { get; set; } = null!;

        [ObservableProperty]
        [Required]
        private string _description = string.Empty;

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value)) // Si el valor realmente cambió...
                {
                    OnPropertyChanged(nameof(LineTotal)); // <-- Notifica a LineTotal
                }
            }
        }

        private decimal _unitPrice;
        [Column(TypeName = "decimal(18, 2)")]
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (SetProperty(ref _unitPrice, value)) // Si el valor realmente cambió...
                {
                    OnPropertyChanged(nameof(LineTotal)); // <-- Notifica a LineTotal
                }
            }
        }

        // Propiedad LineTotal calculada (sin cambios)
        public decimal LineTotal => Quantity * UnitPrice;

        // ELIMINAR ESTOS MÉTODOS PARCIALES:
        // partial void OnQuantityChanged(int value) { ... }
        // partial void OnUnitPriceChanged(decimal value) { ... }
    }
}