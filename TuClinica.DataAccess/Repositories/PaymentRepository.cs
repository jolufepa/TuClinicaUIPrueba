using Microsoft.EntityFrameworkCore;
using System; // Añadido para DateTime
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

        public async Task<decimal> GetTotalPaidForPatientAsync(int patientId)
        {
            var total = await _context.Payments
                .Where(p => p.PatientId == patientId)
                .SumAsync(p => (double)p.Amount);

            return (decimal)total;
        }

        // --- IMPLEMENTACIÓN NUEVA ---
        public async Task<IEnumerable<Payment>> GetByDateRangeAsync(DateTime start, DateTime end)
        {
            var actualEnd = end.Date.AddDays(1).AddTicks(-1);

            return await _context.Payments
                .Include(p => p.Patient) // Vital
                .Where(p => p.PaymentDate >= start.Date && p.PaymentDate <= actualEnd)
                .OrderBy(p => p.PaymentDate)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}