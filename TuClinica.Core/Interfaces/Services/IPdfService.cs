using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TuClinica.Core.Models;

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

        // Método existente
        Task<byte[]> GenerateHistoryPdfAsync(Patient patient, List<ClinicalEntry> entries, List<Payment> payments, decimal totalBalance);

        // --- NUEVO MÉTODO PARA REPORTE FINANCIERO ---
        Task<string> GenerateFinancialReportPdfAsync(DateTime startDate, DateTime endDate, List<FinancialTransactionDto> transactions);
    }
}