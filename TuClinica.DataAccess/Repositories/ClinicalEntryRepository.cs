using Microsoft.EntityFrameworkCore;
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
        public async Task<bool> DeleteEntryAndAllocationsAsync(int entryId)
        {
            // Iniciar una transacción para asegurar que todo se haga o nada se haga
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Encontrar el cargo que se va a eliminar
                var entryToDelete = await _context.ClinicalEntries
                    .FirstOrDefaultAsync(c => c.Id == entryId);

                if (entryToDelete == null)
                {
                    // No se encontró nada que borrar
                    await transaction.RollbackAsync();
                    return false;
                }

                // 2. Encontrar TODAS las asignaciones de pago para este cargo
                var allocationsToDelete = await _context.PaymentAllocations
                    .Where(a => a.ClinicalEntryId == entryId)
                    .ToListAsync();

                // 3. Eliminar las asignaciones
                // (Esto "devuelve" el dinero a los pagos originales)
                if (allocationsToDelete.Any())
                {
                    _context.PaymentAllocations.RemoveRange(allocationsToDelete);
                }

                // 4. Eliminar el cargo en sí
                _context.ClinicalEntries.Remove(entryToDelete);

                // 5. Guardar todos los cambios (borrado de cargo Y asignaciones)
                await _context.SaveChangesAsync();

                // 6. Confirmar la transacción
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception)
            {
                // Si algo sale mal, deshacer todo
                await transaction.RollbackAsync();
                return false;
            }
        }
    }
}