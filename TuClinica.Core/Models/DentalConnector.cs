using System;
using System.Collections.Generic;

namespace TuClinica.Core.Models
{
    public enum ConnectorType
    {
        Puente,
        Ortodoncia,
        Ferulizacion
    }

    public class DentalConnector
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public ConnectorType Type { get; set; }

        // La secuencia ordenada de dientes por donde pasa el arco (ej: 13, 12, 11, 21)
        public List<int> ToothSequence { get; set; } = new List<int>();

        // Guardamos el color como HEX string para no ensuciar el Core con librerías de UI
        public string ColorHex { get; set; } = "#3498DB";
        public double Thickness { get; set; } = 3.0;
    }
}