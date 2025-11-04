using Microsoft.EntityFrameworkCore; // Para Include, FirstOrDefaultAsync, etc.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories; // Importa IBudgetRepository
using TuClinica.Core.Models; // Importa Budget, BudgetLineItem
using TuClinica.Core.Enums; // *** AÑADIR ESTA IMPORTACIÓN ***
using TuClinica.DataAccess; // Importa AppDbContext

namespace TuClinica.DataAccess.Repositories
{
    public class BudgetRepository : Repository<Budget>, IBudgetRepository
    {
        public BudgetRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<string> GetNextBudgetNumberAsync()
        {
            int currentYear = DateTime.Now.Year;
            // Formato del prefijo: AÑO- (ej: "2025-")
            string yearPrefix = $"{currentYear}-";

            // Buscamos el último presupuesto de ESTE año, ordenado por número descendente
            var lastBudget = await _context.Budgets
                .Where(b => b.BudgetNumber.StartsWith(yearPrefix))
                .OrderByDescending(b => b.BudgetNumber)
                .AsNoTracking() // No necesitamos rastrear esta entidad
                .FirstOrDefaultAsync();

            int nextSequential = 1;
            if (lastBudget != null)
            {
                // Extraemos la parte numérica (ej: "0042" de "2025-0042")
                string numberPart = lastBudget.BudgetNumber.Substring(yearPrefix.Length);
                if (int.TryParse(numberPart, out int lastSequential))
                {
                    nextSequential = lastSequential + 1;
                }
                // Si TryParse falla (datos corruptos?), empezamos de 1 igualmente
            }

            // Devolvemos el número formateado con 4 dígitos (ej: "2025-0001")
            return $"{yearPrefix}{nextSequential:D4}";
        }

        public async Task<IEnumerable<Budget>> FindBudgetsAsync(/* filtros */)
        {
            // Por ahora, devolvemos todos ordenados por fecha descendente
            // Incluimos al Paciente para mostrar su nombre en el historial
            return await _context.Budgets
                                 .Include(b => b.Patient) // Carga los datos del paciente relacionado
                                 .OrderByDescending(b => b.IssueDate)
                                 .AsNoTracking()
                                 .ToListAsync();
            // TODO: Añadir filtros por paciente, rango de fechas, estado, etc.
        }

        public async Task<Budget?> GetBudgetWithDetailsAsync(int budgetId)
        {
            // Obtenemos un presupuesto específico incluyendo sus líneas de detalle
            // y los datos del paciente. Es importante usar Include para cargar datos relacionados.
            return await _context.Budgets
                                 .Include(b => b.Patient)     // Carga el Paciente
                                 .Include(b => b.Items)       // Carga la colección de BudgetLineItems
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(b => b.Id == budgetId);
        }

        // *** AÑADIR ESTE MÉTODO ***
        public async Task UpdateStatusAsync(int budgetId, BudgetStatus newStatus)
        {
            // Buscamos el presupuesto por su ID.
            // ¡Importante! NO usamos AsNoTracking() porque queremos que EF rastree los cambios.
            var budgetToUpdate = await _context.Budgets
                                               .FirstOrDefaultAsync(b => b.Id == budgetId);

            if (budgetToUpdate != null)
            {
                // 1. Modificamos la propiedad
                budgetToUpdate.Status = newStatus;

                // 2. Guardamos los cambios en la base de datos
                await _context.SaveChangesAsync();
            }
            // Si no se encuentra, simplemente no hace nada.
        }
    }
}