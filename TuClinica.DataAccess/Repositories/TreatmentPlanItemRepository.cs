// En: TuClinica.DataAccess/Repositories/TreatmentPlanItemRepository.cs
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Models;
using TuClinica.DataAccess;

namespace TuClinica.DataAccess.Repositories
{
    public class TreatmentPlanItemRepository : Repository<TreatmentPlanItem>, ITreatmentPlanItemRepository
    {
        public TreatmentPlanItemRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<TreatmentPlanItem>> GetTasksForPatientAsync(int patientId)
        {
            return await _context.TreatmentPlanItems
                .Where(t => t.PatientId == patientId)
                .OrderByDescending(t => t.DateAdded)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}