// En: TuClinica.UI/Services/InactivityService.cs
using System;
using System.Windows.Threading;
using TuClinica.Core.Interfaces.Services;

namespace TuClinica.UI.Services
{
    public class InactivityService : IInactivityService
    {
        private readonly TimeSpan _timeout = TimeSpan.FromMinutes(15);
        private readonly DispatcherTimer _timer;

        public event Action? OnInactivity;

        public InactivityService()
        {
            _timer = new DispatcherTimer
            {
                Interval = _timeout
            };
            _timer.Tick += Timer_Tick;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            Stop();
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
            if (_timer.IsEnabled)
            {
                _timer.Stop();
                _timer.Start();
            }
        }
    }
}