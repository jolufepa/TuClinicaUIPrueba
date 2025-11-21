using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TuClinica.Core.Enums;

namespace TuClinica.Core.Models
{
    public class PatientFile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PatientId { get; set; }
        [ForeignKey("PatientId")]
        public Patient? Patient { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty; // Nombre visible (ej: "RGPD 2025")

        [Required]
        [MaxLength(500)]
        public string RelativePath { get; set; } = string.Empty; // Ruta interna (ej: "10/guid.pdf")

        public FileCategory Category { get; set; } = FileCategory.Otro;

        public DateTime UploadDate { get; set; } = DateTime.Now;
    }
}