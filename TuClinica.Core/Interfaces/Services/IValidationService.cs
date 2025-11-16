// En: TuClinica.Core/Interfaces/Services/IValidationService.cs
using TuClinica.Core.Enums; // <-- AÑADIR ESTE USING

namespace TuClinica.Core.Interfaces.Services
{
    public interface IValidationService
    {
        bool IsValidDniNie(string dni);

        // --- AÑADIR ESTE NUEVO MÉTODO ---
        /// <summary>
        /// Valida un número de documento basado en su tipo.
        /// </summary>
        /// <param name="documentNumber">El número (ej. "12345678Z" o "Y-123456")</param>
        /// <param name="type">El tipo de documento (DNI, NIE, Pasaporte, Otro)</param>
        /// <returns>True si es válido, False si no.</returns>
        bool IsValidDocument(string documentNumber, PatientDocumentType type);
    }
}