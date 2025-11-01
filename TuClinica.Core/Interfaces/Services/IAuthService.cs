using System.Threading.Tasks;
using TuClinica.Core.Models; // Necesitamos el modelo User

namespace TuClinica.Core.Interfaces.Services
{
    public interface IAuthService
    {
        // Propiedad para saber quién está logueado (si hay alguien)
        User? CurrentUser { get; }

        // Método para intentar iniciar sesión
        Task<bool> LoginAsync(string username, string password);

        // Método para cerrar sesión
        void Logout();
    }
}