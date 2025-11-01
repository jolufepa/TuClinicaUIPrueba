using System.Linq;
using System.Text.RegularExpressions;

using TuClinica.Core.Interfaces.Services; //
namespace TuClinica.Services.Implementation
{
    // Esta clase implementa el contrato IValidationService
    public class ValidationService : IValidationService
    {
        public bool IsValidDniNie(string nif)
        {
            if (string.IsNullOrWhiteSpace(nif))
                return false;

            nif = nif.ToUpper().Trim();

            // Usamos una expresión regular para comprobar el formato (8 números + 1 letra ó X/Y/Z + 7 números + 1 letra)
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
                return false; // No se pudo convertir el número
            }

            // Comprobamos si la letra del NIF coincide con la letra calculada
            return nif.Last() == letraCalculada;
        }
    }
}
