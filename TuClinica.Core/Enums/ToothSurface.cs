using System;

namespace TuClinica.Core.Enums
{
    [Flags]
    public enum ToothSurface
    {
        Ninguna = 0,
        Oclusal = 1,
        Mesial = 2,
        Distal = 4,
        Vestibular = 8,
        Lingual = 16,
        Palatino = 16, // Alias para Lingual en superiores
        Completo = 32
    }
}