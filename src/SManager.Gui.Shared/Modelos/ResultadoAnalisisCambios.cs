namespace SManager.Gui.Shared.Modelos;

/// <summary>
/// Representa un archivo concreto que sufrirá una acción (nuevo, modificado o eliminación espejo)
/// durante la vista previa de «Analizar cambios».
/// </summary>
public sealed record ArchivoCambioPrevisto(
    string NombrePar,
    string RutaRelativa,
    string TipoAccion,
    long TamanoBytes);

/// <summary>Resultado del análisis simulado de un par (sin copiar ni borrar archivos).</summary>
public sealed class ResultadoAnalisisPar
{
    public string IdPar { get; init; } = string.Empty;

    public string NombrePar { get; init; } = string.Empty;

    public int ArchivosNuevos { get; set; }

    public int ArchivosModificados { get; set; }

    public int ArchivosHuerfanosEspejo { get; set; }

    public int OmitidosPorFiltro { get; set; }

    public int YaSincronizados { get; set; }

    public int ErroresAcceso { get; set; }

    public long BytesPendientes { get; set; }

    public long BytesNuevos { get; set; }

    public long BytesModificados { get; set; }

    public long BytesHuerfanosEspejo { get; set; }

    public long BytesOmitidosFiltro { get; set; }

    public IReadOnlyList<string> AvisosRiesgo { get; init; } = [];

    /// <summary>Lista detallada de archivos que cambiarían (hasta el límite de visualización por par).</summary>
    public List<ArchivoCambioPrevisto> ArchivosPrevistos { get; init; } = [];

    /// <summary>Indica si hubo más archivos pendientes que el límite de muestra en pantalla.</summary>
    public bool DetalleTruncado { get; set; }

    /// <summary>Archivos que se copiarían en una sincronización real.</summary>
    public int TotalPendientes => ArchivosNuevos + ArchivosModificados;
}

/// <summary>Agregado del análisis de todos los pares solicitados.</summary>
public sealed class ResultadoAnalisisGlobal
{
    public IReadOnlyList<ResultadoAnalisisPar> PorPar { get; init; } = [];

    public int TotalNuevos => PorPar.Sum(p => p.ArchivosNuevos);

    public int TotalModificados => PorPar.Sum(p => p.ArchivosModificados);

    public int TotalHuerfanosEspejo => PorPar.Sum(p => p.ArchivosHuerfanosEspejo);

    public int TotalOmitidos => PorPar.Sum(p => p.OmitidosPorFiltro);

    public int TotalErrores => PorPar.Sum(p => p.ErroresAcceso);

    public long TotalBytesPendientes => PorPar.Sum(p => p.BytesPendientes);

    public long TotalBytesNuevos => PorPar.Sum(p => p.BytesNuevos);

    public long TotalBytesModificados => PorPar.Sum(p => p.BytesModificados);

    public long TotalBytesHuerfanosEspejo => PorPar.Sum(p => p.BytesHuerfanosEspejo);

    /// <summary>Todos los archivos previstos combinados de los pares analizados.</summary>
    public IReadOnlyList<ArchivoCambioPrevisto> TodosLosArchivosPrevistos =>
        PorPar.SelectMany(p => p.ArchivosPrevistos).ToList();

    public bool AlgunDetalleTruncado => PorPar.Any(p => p.DetalleTruncado);
}
