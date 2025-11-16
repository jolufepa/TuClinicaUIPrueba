// En: TuClinica.Services/Implementation/ValidationService.cs
using System.Linq;
using System.Text.RegularExpressions;
using TuClinica.Core.Enums; // <-- AÑADIR ESTE USING
using TuClinica.Core.Interfaces.Services;

namespace TuClinica.Services.Implementation
{
    public class ValidationService : IValidationService
    {
        // --- INICIO DE LA MODIFICACIÓN ---
        public bool IsValidDocument(string documentNumber, PatientDocumentType type)
        {
            if (string.IsNullOrWhiteSpace(documentNumber))
                return false;

            switch (type)
            {
                case PatientDocumentType.DNI:
                case PatientDocumentType.NIE:
                    // Usamos la validación estricta que ya teníamos
                    return IsValidDniNie(documentNumber);

                case PatientDocumentType.Pasaporte:
                case PatientDocumentType.Otro:
                    // Para pasaporte u otro, solo pedimos un mínimo de 4 caracteres
                    return documentNumber.Length >= 4;

                default:
                    return false;
            }
        }
        // --- FIN DE LA MODIFICACIÓN ---

        public bool IsValidDniNie(string nif)
        {
            if (string.IsNullOrWhiteSpace(nif))
                return false;

            nif = nif.ToUpper().Trim();

            if (!Regex.IsMatch(nif, @"^(\d{8}[A-Z]|[XYZ]\d{7}[A-Z])$"))
                return false;

            string letras = "TRWAGMYFPDXBNJZSQVHLCKE";
            char letraCalculada;
            string num;

            if (nif[0] == 'X')
                num = "0" + nif.Substring(1, 7);
            else if (nif[0] == 'Y')
                num = "1" + nif.Substring(1, 7);
            else if (nif[0] == 'Z')
                num = "2" + nif.Substring(1, 7);
            else
                num = nif.Substring(0, 8);

            if (int.TryParse(num, out int dniNum))
            {
                letraCalculada = letras[dniNum % 23];
            }
            else
            {
                return false;
            }

            return nif.Last() == letraCalculada;
        }
    }
}