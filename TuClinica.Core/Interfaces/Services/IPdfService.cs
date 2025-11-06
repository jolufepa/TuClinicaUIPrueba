// Contenido existente...
using System.Threading.Tasks;
using TuClinica.Core.Models; // Necesitamos Budget
// --- AÑADE ESTOS USINGS ---
using System.Collections.ObjectModel;
using TuClinica.Core.Models; // Para Patient (ya debería estar)

namespace TuClinica.Core.Interfaces.Services
{
    public interface IPdfService
    {
        // Método existente
        Task<string> GenerateBudgetPdfAsync(Budget budget);

        // Método existente
        Task<string> GeneratePrescriptionPdfAsync(Prescription prescription);

        // --- AÑADE ESTE NUEVO MÉTODO ---
        /// <summary>
        /// Genera un PDF profesional del odontograma de un paciente.
        /// </summary>
        /// <param name="patient">El paciente al que pertenece el odontograma.</param>
        /// <param name="odontogramJsonState">El estado del odontograma serializado como JSON.</param>
        /// <returns>La ruta al archivo PDF generado.</returns>
        Task<string> GenerateOdontogramPdfAsync(Patient patient, string odontogramJsonState);
    }
}