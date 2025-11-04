using Microsoft.EntityFrameworkCore;
using System.Collections.Generic; // Para IEnumerable y List
using System.Linq; // Para OrderBy
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Models;
using TuClinica.DataAccess;

namespace TuClinica.DataAccess.Repositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            // Buscamos ignorando mayúsculas/minúsculas y solo activos
            return await _context.Users
                                 .FirstOrDefaultAsync(u => u.IsActive && u.Username.ToLower() == username.ToLower());
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            // Devolvemos todos los usuarios ordenados por nombre de usuario.
            // Usamos AsNoTracking() porque solo los vamos a mostrar, no a modificar directamente aquí.
            return await _context.Users
                                 .OrderBy(u => u.Username)
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        // *** MÉTODO UpdateAsync CORREGIDO ***
        public async Task UpdateAsync(User user)
        {
            // 1. Busca la entidad original en la base de datos (¡CON seguimiento!)
            var existingUser = await _context.Users.FindAsync(user.Id);

            if (existingUser != null)
            {
                // 2. Copia las propiedades modificadas desde 'user' (el objeto editado)
                //    a 'existingUser' (el objeto rastreado por EF Core).
                //    ¡NO actualizamos el HashedPassword si no se proporcionó uno nuevo!
                //    (La lógica de si se proporcionó o no ya está en UserEditViewModel)

                _context.Entry(existingUser).CurrentValues.SetValues(user);

                // Si el UserEditViewModel puso un HashedPassword vacío en 'user'
                // porque no se cambió, SetValues lo copiaría. Debemos restaurar el original si es necesario.
                // ¡OJO! Esta lógica es mejor manejarla en el ViewModel, asegurándose
                // de que 'user.HashedPassword' solo tenga valor si realmente se cambió.

                // Asumamos que UserEditViewModel ya puso el HashedPassword correcto en 'user'
                // (ya sea el nuevo hash o el hash original si no se cambió).

                // 3. Guarda los cambios
                await _context.SaveChangesAsync();
            }
            // else: El usuario no fue encontrado, podríamos lanzar una excepción o ignorarlo.
        }

        public async Task<bool> IsUsernameTakenAsync(string username, int userIdToExclude = 0)
        {
            // Busca si existe algún usuario con ese nombre (ignorando mayúsculas/minúsculas)
            // Y que tenga un ID diferente al que estamos editando (userIdToExclude)
            return await _context.Users
                                 .AnyAsync(u => u.Username.ToLower() == username.ToLower()
                                             && u.Id != userIdToExclude);
        }
    } // Fin de la clase UserRepository
} // Fin del namespace TuClinica.DataAccess.Repositories

