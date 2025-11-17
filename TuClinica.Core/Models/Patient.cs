// En: TuClinica.Core/Models/Patient.cs
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization;
using TuClinica.Core.Enums;
using System.Collections.Generic;


namespace TuClinica.Core.Models
{
    public partial class Patient : ObservableValidator
    {
        [Key]
        [ObservableProperty]
        private int _id;

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [ObservableProperty]
        private string _name = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        [ObservableProperty]
        private string _surname = string.Empty;


        // --- INICIO DE LA MODIFICACIÓN ---

        [ObservableProperty]
        private PatientDocumentType _documentType; // <-- AÑADIDO

        [Required(ErrorMessage = "El número de documento es obligatorio.")]
        [ObservableProperty]
        private string _documentNumber = string.Empty; // <-- RENOMBRADO (antes _dniNie)

        // --- FIN DE LA MODIFICACIÓN ---

        [ObservableProperty]
        [CustomValidation(typeof(TuClinica.Core.Models.ValidationRules), nameof(TuClinica.Core.Models.ValidationRules.IsValidDateOfBirth))]
        private DateTime? _dateOfBirth;

        [ObservableProperty]
        private string? _phone;

        [ObservableProperty]
        private string? _address;

        [RegularExpression(@"^$|^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "El formato del email no es válido.")]
        private string? _email;
        public string? Email
        {
            get => _email;
            set
            {
                var trimmedValue = value?.Trim();
                SetProperty(ref _email, trimmedValue, true);
            }
        }

        [ObservableProperty]
        private string? _notes;

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private string? _odontogramStateJson;
        public ICollection<LinkedDocument> LinkedDocuments { get; set; } = new List<LinkedDocument>();
        /// <summary>
        /// Colección de alertas médicas importantes para este paciente.
        /// </summary>
        public ICollection<PatientAlert> Alerts { get; set; } = new List<PatientAlert>();

        /// Colección de documentos históricos o secundarios asociados a este paciente.
        /// El documento principal y actual siempre está en DocumentType y DocumentNumber.
        /// </summary>
       

        [NotMapped]
        [ReadOnly(true)]
        [Browsable(false)]
        [JsonIgnore]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        // --- MODIFICADO para usar DocumentNumber ---
        public string PatientDisplayInfo => $"{Name} {Surname} ({DocumentNumber})";


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
                // --- MODIFICADO ---
                DocumentType = this.DocumentType,
                DocumentNumber = this.DocumentNumber,
                // ---
                DateOfBirth = this.DateOfBirth,
                Phone = this.Phone,
                Address = this.Address,
                Email = this.Email,
                Notes = this.Notes,
                IsActive = this.IsActive,
                OdontogramStateJson = this.OdontogramStateJson
                // Omitimos la colección de LinkedDocuments a propósito
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
            // --- MODIFICADO ---
            this.DocumentType = source.DocumentType;
            this.DocumentNumber = source.DocumentNumber;
            // ---
            this.DateOfBirth = source.DateOfBirth;
            this.Phone = source.Phone;
            this.Address = source.Address;
            this.Email = source.Email;
            this.Notes = source.Notes;
            this.IsActive = source.IsActive;
            this.OdontogramStateJson = source.OdontogramStateJson;
        }

        public void ForceValidation()
        {
            base.ValidateAllProperties();
        }
    }
}