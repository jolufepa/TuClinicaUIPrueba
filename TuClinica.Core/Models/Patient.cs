using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel; // VITAL
using System.Diagnostics;    // VITAL
using System.Text.Json.Serialization;

namespace TuClinica.Core.Models
{
    public class Patient
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Surname { get; set; } = string.Empty;

        [Required]
        public string DniNie { get; set; } = string.Empty;

        // Propiedades opcionales
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        [NotMapped]
        [ReadOnly(true)]
        [Browsable(false)]
        [JsonIgnore]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        // <--- ¡CAMBIO CLAVE! ELIMINAMOS EL NOMBRE CONFLICTIVO
        public string PatientDisplayInfo => $"{Name} {Surname} ({DniNie})";
    }
}