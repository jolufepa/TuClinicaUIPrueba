using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Models;
using TuClinica.DataAccess;

namespace TuClinica.DataAccess.Repositories
{
    public class ClinicalEntryRepository : Repository<ClinicalEntry>, IClinicalEntryRepository
    {
        public ClinicalEntryRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<ClinicalEntry>> GetHistoryForPatientAsync(int patientId)
        {
            return await _context.ClinicalEntries
                .Where(c => c.PatientId == patientId)
                .Include(c => c.TreatmentsPerformed)
                .Include(c => c.Allocations)
                .Include(c => c.Doctor)
                .OrderByDescending(c => c.VisitDate)
                .AsSplitQuery()
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<decimal> GetTotalChargedForPatientAsync(int patientId)
        {
            var total = await _context.ClinicalEntries
                .Where(c => c.PatientId == patientId)
                .SumAsync(c => (double)c.TotalCost);

            return (decimal)total;
        }

        // --- IMPLEMENTACIÓN NUEVA ---
        public async Task<IEnumerable<ClinicalEntry>> GetByDateRangeAsync(DateTime start, DateTime end)
        {
            // Ajustamos 'end' para incluir todo el último día (hasta 23:59:59)
            var actualEnd = end.Date.AddDays(1).AddTicks(-1);

            return await _context.ClinicalEntries
                .Include(c => c.Patient) // Vital para mostrar el nombre
                .Where(c => c.VisitDate >= start.Date && c.VisitDate <= actualEnd)
                .OrderBy(c => c.VisitDate)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<bool> DeleteEntryAndAllocationsAsync(int entryId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var entryToDelete = await _context.ClinicalEntries
                    .FirstOrDefaultAsync(c => c.Id == entryId);

                if (entryToDelete == null)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                var allocationsToDelete = await _context.PaymentAllocations
                    .Where(a => a.ClinicalEntryId == entryId)
                    .ToListAsync();

                if (allocationsToDelete.Any())
                {
                    _context.PaymentAllocations.RemoveRange(allocationsToDelete);
                }

                _context.ClinicalEntries.Remove(entryToDelete);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return false;
            }
        }
    }
}