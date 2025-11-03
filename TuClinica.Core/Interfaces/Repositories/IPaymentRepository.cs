using System.Collections.Generic;
using System.Threading.Tasks;
using TuClinica.Core.Models;

namespace TuClinica.Core.Interfaces.Repositories
{
    public interface IPaymentRepository : IRepository<Payment>
    {
        Task<IEnumerable<Payment>> GetPaymentsForPatientAsync(int patientId);
    }
}