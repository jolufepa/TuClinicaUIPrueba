using TuClinica.Core.Interfaces;
using TuClinica.Core.Models;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username);
    Task<IEnumerable<User>> GetAllAsync();
    Task UpdateAsync(User user);

    // *** AÑADIR ESTA LÍNEA ***
    Task<bool> IsUsernameTakenAsync(string username, int userIdToExclude = 0);
}