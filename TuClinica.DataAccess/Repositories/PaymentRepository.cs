using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Models;
using TuClinica.DataAccess;

namespace TuClinica.DataAccess.Repositories
{
    public class PaymentRepository : Repository<Payment>, IPaymentRepository
    {
        public PaymentRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Payment>> GetPaymentsForPatientAsync(int patientId)
        {
            return await _context.Payments
                .Where(p => p.PatientId == patientId)
                .Include(p => p.Allocations)
                .OrderByDescending(p => p.PaymentDate)
                .AsNoTracking()
                .ToListAsync();
        }

        // --- IMPLEMENTACIÓN DE LA MEJORA (CORREGIDA PARA SQLITE) ---
        public async Task<decimal> GetTotalPaidForPatientAsync(int patientId)
        {
            // 1. Convertimos 'Amount' (decimal) a 'double' DENTRO de la consulta.
            var total = await _context.Payments
                .Where(p => p.PatientId == patientId)
                .SumAsync(p => (double)p.Amount); // SQLite suma 'double'

            // 2. Convertimos el resultado 'double' de vuelta a 'decimal'
            return (decimal)total;
        }
    }
}