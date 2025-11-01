using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TuClinica.Core.Models
{
    public class Medication
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } // Ej: "Amoxicilina"

        [MaxLength(100)]
        public string? Presentation { get; set; } // Ej: "750mg Comprimidos"
        [NotMapped]
        public string FullDisplay => string.IsNullOrWhiteSpace(Presentation) ? Name : $"{Name} ({Presentation})";

        public bool IsActive { get; set; } = true;
    }
}