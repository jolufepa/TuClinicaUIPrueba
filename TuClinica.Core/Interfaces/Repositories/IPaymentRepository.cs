using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TuClinica.Core.Models;

namespace TuClinica.Core.Interfaces.Repositories
{
    public interface IPaymentRepository : IRepository<Payment>
    {
        Task<IEnumerable<Payment>> GetPaymentsForPatientAsync(int patientId);

        // --- NUEVO MÉTODO ---
        /// <summary>
        /// Calcula la suma total de los pagos del paciente directamente en la BD.
        /// </summary>
        Task<decimal> GetTotalPaidForPatientAsync(int patientId);

        // --- NUEVO MÉTODO PARA REPORTE ---
        Task<IEnumerable<Payment>> GetByDateRangeAsync(DateTime start, DateTime end);
    }
}