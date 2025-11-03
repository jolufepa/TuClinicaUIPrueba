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
    }
}