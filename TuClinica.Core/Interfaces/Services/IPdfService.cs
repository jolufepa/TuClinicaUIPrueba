using System.Threading.Tasks;
using TuClinica.Core.Models;
using System.Collections.ObjectModel;
using System.Collections.Generic; // <-- AÑADIR ESTE USING

namespace TuClinica.Core.Interfaces.Services
{
    public interface IPdfService
    {
        // Método existente
        Task<string> GenerateBudgetPdfAsync(Budget budget);

        // Método existente
        Task<string> GeneratePrescriptionPdfAsync(Prescription prescription);

        // Método existente
        Task<string> GenerateBasicPrescriptionPdfAsync(Prescription prescription);

        // Método existente
        Task<string> GenerateOdontogramPdfAsync(Patient patient, string odontogramJsonState);

        // --- AÑADIR ESTE NUEVO MÉTODO ---
        /// <summary>
        /// Genera un PDF del historial de contabilidad del paciente en memoria.
        /// </summary>
        /// <param name="patient">Datos del paciente</param>
        /// <param name="entries">Lista de todos los cargos</param>
        /// <param name="payments">Lista de todos los pagos</param>
        /// <param name="totalBalance">El saldo total calculado</param>
        /// <returns>Un array de bytes (byte[]) con el contenido del PDF.</returns>
        Task<byte[]> GenerateHistoryPdfAsync(Patient patient, List<ClinicalEntry> entries, List<Payment> payments, decimal totalBalance);
    }
}