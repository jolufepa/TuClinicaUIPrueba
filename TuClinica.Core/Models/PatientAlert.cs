using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TuClinica.Core.Enums;

namespace TuClinica.Core.Models
{
    /// <summary>
    /// Representa una alerta médica importante para un paciente.
    /// </summary>
    public class PatientAlert
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PatientId { get; set; }
        [ForeignKey("PatientId")]
        public Patient? Patient { get; set; }

        [Required]
        [MaxLength(200)]
        public string Message { get; set; } = string.Empty;

        [Required]
        public AlertLevel Level { get; set; } = AlertLevel.Info;

        public bool IsActive { get; set; } = true;
    }
}