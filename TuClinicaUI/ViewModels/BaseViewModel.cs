using CommunityToolkit.Mvvm.ComponentModel; // ¡Importante!

namespace TuClinica.UI.ViewModels
{
    // Esta clase nos da la habilidad de notificar a la UI
    // cuando una propiedad cambia (ej: el nombre de un paciente).
    public abstract class BaseViewModel : ObservableObject
    {
        // Esta será la base para TODOS nuestros ViewModels
    }
}