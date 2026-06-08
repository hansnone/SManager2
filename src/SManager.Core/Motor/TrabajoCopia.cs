namespace SManager.Core.Motor;

/// <summary>Trabajo encolado para el pool de copiadores.</summary>
public sealed record TrabajoCopia(string IdPar, string RutaCompleta);

/// <summary>Trabajo encolado para hidratar un placeholder OneDrive.</summary>
public sealed record TrabajoHidratacion(string IdPar, string RutaCompleta);

/// <summary>Evento de estadísticas por par tras una copia o error.</summary>
public sealed record EstadisticaPar(
    string IdPar,
    int Copiados,
    int Errores,
    DateTime? UltimaSincronizacion,
    string Estado);

/// <summary>Entrada de actividad reciente para el monitor.</summary>
public sealed record EntradaActividadInterna(
    string Hora,
    string Tipo,
    string Archivo,
    string IdPar,
    string? Detalle = null);

/// <summary>Copia en curso publicada en telemetría.</summary>
public sealed record CopiaEnCursoInterna(
    string Archivo,
    string IdPar,
    int Copiador);
