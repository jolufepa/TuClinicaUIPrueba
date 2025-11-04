// TuClinica.Core/Enums/ToothCondition.cs
namespace TuClinica.Core.Enums
{
    public enum ToothCondition
    {
        Sano,
        Caries,
        ExtraccionIndicada,
        Ausente, // Diente que nunca existió (agenesia) o se perdió
        Fractura,
        // Puedes añadir más: Hipoplasia, Erosión, etc.
    }
}