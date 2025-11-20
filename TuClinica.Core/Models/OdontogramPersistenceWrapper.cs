using System.Collections.Generic;
using TuClinica.Core.Enums;

namespace TuClinica.Core.Models
{
    /// <summary>
    /// Contenedor para la persistencia del estado del odontograma (JSON).
    /// Incluye versionado para evitar incompatibilidades futuras.
    /// </summary>
    public class OdontogramPersistenceWrapper
    {
        // --- MEJORA DE SEGURIDAD: VERSIONADO ---
        // Esta es la propiedad que faltaba y causaba el error
        public int SchemaVersion { get; set; } = 1;

        public List<ToothStateDto> Teeth { get; set; } = new();
        public List<DentalConnector> Connectors { get; set; } = new();
    }

    public class ToothStateDto
    {
        public int ToothNumber { get; set; }

        public ToothCondition FullCondition { get; set; }
        public ToothCondition OclusalCondition { get; set; }
        public ToothCondition MesialCondition { get; set; }
        public ToothCondition DistalCondition { get; set; }
        public ToothCondition VestibularCondition { get; set; }
        public ToothCondition LingualCondition { get; set; }

        public ToothRestoration FullRestoration { get; set; }
        public ToothRestoration OclusalRestoration { get; set; }
        public ToothRestoration MesialRestoration { get; set; }
        public ToothRestoration DistalRestoration { get; set; }
        public ToothRestoration VestibularRestoration { get; set; }
        public ToothRestoration LingualRestoration { get; set; }
    }
}