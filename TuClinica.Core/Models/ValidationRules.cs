// En: TuClinica.Core/Models/ValidationRules.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace TuClinica.Core.Models
{
    /// <summary>
    /// Contiene métodos de validación personalizados para los modelos.
    /// </summary>
    public class ValidationRules
    {
        /// <summary>
        /// Comprueba si la fecha de nacimiento es válida (no futura).
        /// Permite valores nulos (no es obligatorio).
        /// </summary>
        public static ValidationResult IsValidDateOfBirth(DateTime? date, ValidationContext context)
        {
            // Si el usuario introdujo una fecha
            if (date.HasValue)
            {
                // Y esa fecha es en el futuro
                if (date.Value > DateTime.Now)
                {
                    // Devuelve un error
                    return new ValidationResult("La fecha de nacimiento no puede ser futura.");
                }
            }

            // Si es nulo o es una fecha pasada/presente, es válido
            return ValidationResult.Success;
        }
    }
}