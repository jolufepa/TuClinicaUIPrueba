using System.ComponentModel.DataAnnotations;

namespace TuClinica.Core.Models
{
    public class Dosage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Pauta { get; set; } // Ej: "1 cada 8 horas"

        public bool IsActive { get; set; } = true;
    }
}