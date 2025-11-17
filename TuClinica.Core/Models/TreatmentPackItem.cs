using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TuClinica.Core.Models
{
    /// <summary>
    /// Representa un componente dentro de un Pack de tratamientos.
    /// Ej: En el Pack "Ortodoncia", este ítem podría ser "Revisiones" con Cantidad = 12.
    /// </summary>
    public class TreatmentPackItem
    {
        [Key]
        public int Id { get; set; }

        // El Tratamiento "Padre" (El Pack)
        [Required]
        public int ParentTreatmentId { get; set; }
        [ForeignKey("ParentTreatmentId")]
        public Treatment? ParentTreatment { get; set; }

        // El Tratamiento "Hijo" (El componente, ej. "Limpieza")
        [Required]
        public int ChildTreatmentId { get; set; }
        [ForeignKey("ChildTreatmentId")]
        public Treatment? ChildTreatment { get; set; }

        // Cuántas unidades de este tratamiento incluye el pack
        public int Quantity { get; set; } = 1;
    }
}