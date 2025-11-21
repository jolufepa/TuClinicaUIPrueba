using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TuClinica.Core.Models;

namespace TuClinica.Core.Interfaces.Services
{
    public interface IPdfService
    {
        Task<string> GenerateBudgetPdfAsync(Budget budget);
        Task<string> GeneratePrescriptionPdfAsync(Prescription prescription);
        Task<string> GenerateBasicPrescriptionPdfAsync(Prescription prescription);
        Task<string> GenerateOdontogramPdfAsync(Patient patient, string odontogramJsonState);
        Task<byte[]> GenerateHistoryPdfAsync(Patient patient, List<ClinicalEntry> entries, List<Payment> payments, decimal totalBalance);
        Task<string> GenerateFinancialReportPdfAsync(DateTime startDate, DateTime endDate, List<FinancialTransactionDto> transactions);

        // --- NUEVO ---
        Task<string> GenerateAttendanceProofAsync(Patient patient, DateTime checkIn, DateTime checkOut);
    }
}