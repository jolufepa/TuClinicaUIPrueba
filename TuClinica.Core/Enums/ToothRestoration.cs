// TuClinica.Core/Enums/ToothRestoration.cs (antes ToothStatus)
namespace TuClinica.Core.Enums
{
    public enum ToothRestoration
    {
        Ninguna, // Usamos 'Ninguna' en lugar de 'Sano'
        Obturacion, // Genérico para resina/amalgama
        Endodoncia,
        Sellador,
        Corona,
        Implante,
        ProtesisFija,
        ProtesisRemovible,
        // Puedes detallar más: ObturacionAmalgama, ObturacionResina
    }
}