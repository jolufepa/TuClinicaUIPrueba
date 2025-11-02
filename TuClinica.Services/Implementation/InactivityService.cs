using System;
using System.Windows.Threading; // Necesario para DispatcherTimer
using TuClinica.Core.Interfaces.Services;

namespace TuClinica.Services.Implementation
{
    public class InactivityService : IInactivityService
    {
        // Define el tiempo de inactividad. 15 minutos es un buen estándar.
        private readonly TimeSpan _timeout = TimeSpan.FromMinutes(15);
        private readonly DispatcherTimer _timer;

        public event Action? OnInactivity;

        public InactivityService()
        {
            // Inicializamos el temporizador
            _timer = new DispatcherTimer
            {
                Interval = _timeout
            };
            _timer.Tick += Timer_Tick;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            // El tiempo ha expirado.
            // 1. Detenemos el timer para que no se repita.
            Stop();
            // 2. Disparamos el evento para que la aplicación reaccione.
            OnInactivity?.Invoke();
        }

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public void Reset()
        {
            // Si el timer está corriendo (es decir, el usuario está logueado)...
            if (_timer.IsEnabled)
            {
                // ...lo reiniciamos.
                _timer.Stop();
                _timer.Start();
            }
        }
    }
}