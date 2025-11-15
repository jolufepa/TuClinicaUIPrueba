// En: TuClinica.Core/Models/TreatmentPlanItem.cs
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TuClinica.Core.Models
{
    /// <summary>
    /// Representa una tarea o nota en el plan de tratamiento de un paciente.
    /// </summary>
    public partial class TreatmentPlanItem : ObservableValidator
    {
        [Key]
        [ObservableProperty]
        private int _id;

        [Required]
        [ObservableProperty]
        private int _patientId;

        [ForeignKey("PatientId")]
        public Patient? Patient { get; set; }

        [Required]
        [MaxLength(500)]
        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private bool _isDone = false;

        [ObservableProperty]
        private DateTime _dateAdded;
    }
}