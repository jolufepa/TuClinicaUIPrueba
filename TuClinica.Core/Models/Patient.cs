using CommunityToolkit.Mvvm.ComponentModel; // <-- Necesario
using System;
using System.ComponentModel.DataAnnotations; // <-- Necesario
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace TuClinica.Core.Models
{
    // CAMBIO CLAVE: ObservableObject -> ObservableValidator
    public partial class Patient : ObservableValidator
    {
        [Key]
        [ObservableProperty]
        private int _id;

        [Required] // <-- Atributo de validación
        [ObservableProperty]
        private string _name = string.Empty;

        [Required] // <-- Atributo de validación
        [ObservableProperty]
        private string _surname = string.Empty;

        [Required] // <-- Atributo de validación
        [ObservableProperty]
        private string _dniNie = string.Empty;

        [ObservableProperty]
        private DateTime? _dateOfBirth;

        [ObservableProperty]
        private string? _phone;

        [ObservableProperty]
        private string? _address;

        [ObservableProperty]
        private string? _email;

        [ObservableProperty]
        private string? _notes;

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private string? _odontogramStateJson;

        [NotMapped]
        [ReadOnly(true)]
        [Browsable(false)]
        [JsonIgnore]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public string PatientDisplayInfo => $"{Name} {Surname} ({DniNie})";


        // --- MÉTODOS AÑADIDOS (Usados por el ViewModel) ---

        /// <summary>
        /// Crea una copia exacta de este paciente.
        /// </summary>
        public Patient DeepCopy()
        {
            return new Patient
            {
                Id = this.Id,
                Name = this.Name,
                Surname = this.Surname,
                DniNie = this.DniNie,
                DateOfBirth = this.DateOfBirth,
                Phone = this.Phone,
                Address = this.Address,
                Email = this.Email,
                Notes = this.Notes,
                IsActive = this.IsActive,
                OdontogramStateJson = this.OdontogramStateJson
            };
        }

        /// <summary>
        /// Copia los valores de otro paciente a este.
        /// </summary>
        public void CopyFrom(Patient source)
        {
            // No cambiamos el Id
            this.Name = source.Name;
            this.Surname = source.Surname;
            this.DniNie = source.DniNie;
            this.DateOfBirth = source.DateOfBirth;
            this.Phone = source.Phone;
            this.Address = source.Address;
            this.Email = source.Email;
            this.Notes = source.Notes;
            this.IsActive = source.IsActive;
            this.OdontogramStateJson = source.OdontogramStateJson;
        }
    }
}