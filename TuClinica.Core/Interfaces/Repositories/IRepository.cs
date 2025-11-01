using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace TuClinica.Core.Interfaces // O TuClinica.Core.Interfaces.Repositories
{
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task AddAsync(T entity);
        Task AddRangeAsync(IEnumerable<T> entities);
        void Remove(T entity); // Suelen ser síncronos
        void RemoveRange(IEnumerable<T> entities); // Suelen ser síncronos
        void Update(T entity); // Suelen ser síncronos

        // *** AÑADIR ESTA LÍNEA ***
        Task<int> SaveChangesAsync(); // Devuelve el número de filas afectadas
    }
}