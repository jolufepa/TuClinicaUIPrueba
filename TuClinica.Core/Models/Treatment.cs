using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic; // <--- Asegúrate de tener esto

namespace TuClinica.Core.Models
{
    public class Treatment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal DefaultPrice { get; set; }

        public bool IsActive { get; set; } = true;

        // --- NUEVA PROPIEDAD ---
        /// <summary>
        /// Si este tratamiento es un "Pack", esta lista contiene sus componentes.
        /// Si la lista está vacía, es un tratamiento individual normal.
        /// </summary>
        public ICollection<TreatmentPackItem> PackItems { get; set; } = new List<TreatmentPackItem>();
    }
}