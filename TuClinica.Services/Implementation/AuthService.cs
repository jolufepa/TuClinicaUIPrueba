// En: TuClinica.Services/Implementation/AuthService.cs

using BCrypt.Net; // ¡Importante para las contraseñas!
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories; // Para IUserRepository
using TuClinica.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using System;// Para User
using TuClinica.Core.Models;

namespace TuClinica.Services.Implementation
{
    public class AuthService : IAuthService

    {
        private readonly IInactivityService _inactivityService;

        // --- CAMBIO 1: Reemplazar IServiceProvider ---
        private readonly IServiceScopeFactory _scopeFactory;

        // Guarda el usuario que ha iniciado sesión
        public User? CurrentUser { get; private set; }

        // --- CAMBIO 2: Actualizar el constructor ---
        public AuthService(IServiceScopeFactory scopeFactory, IInactivityService inactivityService)
        {
            _scopeFactory = scopeFactory;
            _inactivityService = inactivityService; // <-- Esto da error si la línea de arriba no existe
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            // --- CAMBIO 3: Usar la 'Factory' para crear el ámbito ---
            using (var scope = _scopeFactory.CreateScope())
            {
                // Obtenemos IUserRepository DENTRO del scope
                var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

                // El resto de la lógica es igual, pero usamos la variable local 'userRepository'
                var user = await userRepository.GetByUsernameAsync(username);

                // 2. Si no existe o la contraseña no coincide, falla el login
                if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.HashedPassword))
                {
                    CurrentUser = null; // Nos aseguramos de limpiar el usuario actual
                    return false;
                }

                // 3. ¡Éxito! Guardamos el usuario logueado
                CurrentUser = user;
                return true;
            }
        }

        public void Logout()
        {
            _inactivityService.Stop();
            // Limpiamos el usuario actual
            CurrentUser = null;
        }
    }
}