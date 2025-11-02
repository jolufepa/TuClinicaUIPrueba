using System;

namespace TuClinica.Core.Interfaces.Services
{
    public interface IInactivityService
    {
        /// <summary>
        /// Evento que se dispara cuando se alcanza el tiempo límite de inactividad.
        /// </summary>
        event Action OnInactivity;

        /// <summary>
        /// Inicia el temporizador de inactividad (se llama al iniciar sesión).
        /// </summary>
        void Start();

        /// <summary>
        /// Detiene el temporizador (se llama al cerrar sesión o al expirar).
        /// </summary>
        void Stop();

        /// <summary>
        /// Resetea el temporizador (se llama con cualquier actividad del usuario).
        /// </summary>
        void Reset();
    }
}