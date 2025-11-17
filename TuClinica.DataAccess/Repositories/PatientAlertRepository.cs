using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Models;
using TuClinica.DataAccess;

namespace TuClinica.DataAccess.Repositories
{
    public class PatientAlertRepository : Repository<PatientAlert>, IPatientAlertRepository
    {
        public PatientAlertRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<PatientAlert>> GetActiveAlertsForPatientAsync(int patientId, CancellationToken token = default)
        {
            return await _context.PatientAlerts
                .Where(a => a.PatientId == patientId && a.IsActive)
                .OrderByDescending(a => a.Level) // Críticas primero
                .AsNoTracking()
                .ToListAsync(token);
        }
    }
}