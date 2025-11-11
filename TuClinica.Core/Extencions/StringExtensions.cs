// En: TuClinica.Core/Extensions/StringExtensions.cs
using System.Globalization;

namespace TuClinica.Core.Extensions
{
    /// <summary>
    /// Métodos de extensión auxiliares para la clase String.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Convierte un string a "Title Case" (Ej: "juan perez" -> "Juan Perez")
        /// y elimina espacios al principio y al final.
        /// </summary>
        /// <param name="input">El string de entrada.</param>
        /// <returns>El string formateado.</returns>
        public static string ToTitleCase(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input ?? string.Empty; // Devuelve string vacío si es nulo

            // Convierte todo a minúsculas, quita espacios y luego aplica TitleCase
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower().Trim());
        }
    }
}