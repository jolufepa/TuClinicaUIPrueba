using System.ComponentModel.DataAnnotations;
using TuClinica.Core.Enums;

namespace TuClinica.Core.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string HashedPassword { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; }

        public bool IsActive { get; set; } = true;
        [MaxLength(50)] // Opcional: define un límite
        public string? CollegeNumber { get; set; } // Ej: "Col. 12345"

        [MaxLength(100)] // Opcional: define un límite
        public string? Specialty { get; set; } // Ej: "Odontólogo"

        [Required]
        public string Name { get; set; } = string.Empty;
    }
}