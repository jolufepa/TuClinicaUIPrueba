// En: TuClinica.Core/Enums/PatientDocumentType.cs
using System.ComponentModel.DataAnnotations;

namespace TuClinica.Core.Enums
{
    public enum PatientDocumentType
    {
        [Display(Name = "DNI")]
        DNI = 0,

        [Display(Name = "NIE")]
        NIE = 1,

        [Display(Name = "Pasaporte")]
        Pasaporte = 2,

        [Display(Name = "Otro Documento")]
        Otro = 3
    }
}