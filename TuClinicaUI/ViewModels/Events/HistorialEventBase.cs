using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace TuClinica.UI.ViewModels.Events
{
    /// <summary>
    /// Clase base para cualquier evento en la bitácora del paciente.
    /// </summary>
    public abstract class HistorialEventBase : ObservableObject
    {
        public DateTime Timestamp { get; protected set; }
    }
}
