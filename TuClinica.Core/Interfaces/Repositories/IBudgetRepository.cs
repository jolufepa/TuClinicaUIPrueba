using System.Collections.Generic;
using System.Threading.Tasks;
using TuClinica.Core.Models; // Necesitamos Budget
using TuClinica.Core.Enums; // *** AÑADIR ESTA IMPORTACIÓN ***

namespace TuClinica.Core.Interfaces.Repositories
{
    // Hereda de IRepository<Budget>
    public interface IBudgetRepository : IRepository<Budget>
    {
        // Método para obtener el siguiente número de presupuesto para el año actual
        Task<string> GetNextBudgetNumberAsync();

        // Método para buscar presupuestos (para el historial)
        Task<IEnumerable<Budget>> FindBudgetsAsync(/* filtros */);

        // Método para obtener un presupuesto con sus líneas de detalle incluidas
        Task<Budget?> GetBudgetWithDetailsAsync(int budgetId);

        // *** AÑADIR ESTE MÉTODO ***
        // Método para actualizar solo el estado de un presupuesto
        Task UpdateStatusAsync(int budgetId, BudgetStatus newStatus);
    }
}