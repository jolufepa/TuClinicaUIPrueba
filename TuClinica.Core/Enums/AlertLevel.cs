namespace TuClinica.Core.Enums
{
    /// <summary>
    /// Define el nivel de severidad de una alerta médica.
    /// </summary>
    public enum AlertLevel
    {
        /// <summary>
        /// Informativo, no crítico. Ej: "Paciente nervioso"
        /// </summary>
        Info,
        /// <summary>
        /// Requiere precaución. Ej: "Toma Sintrom", "Hipertenso"
        /// </summary>
        Warning,
        /// <summary>
        /// Peligro vital o de procedimiento. Ej: "Alergia Penicilina", "Alergia Latex"
        /// </summary>
        Critical
    }
}