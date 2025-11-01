using System.Threading.Tasks;
using TuClinica.Core.Models; // Necesitamos Budget

namespace TuClinica.Core.Interfaces.Services
{
    public interface IPdfService
    {
        // Método principal para generar el PDF de un presupuesto
        // Devuelve la ruta donde se guardó el archivo
        Task<string> GenerateBudgetPdfAsync(Budget budget);

        // *** AÑADIR ESTA LÍNEA ***
        Task<string> GeneratePrescriptionPdfAsync(Prescription prescription);
    }
}