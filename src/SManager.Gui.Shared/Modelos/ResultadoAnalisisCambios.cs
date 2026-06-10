namespace SManager.Gui.Shared.Modelos;

/// <summary>Resultado del análisis simulado de un par (sin copiar archivos).</summary>
public sealed class ResultadoAnalisisPar
{
    public string IdPar { get; init; } = string.Empty;

    public string NombrePar { get; init; } = string.Empty;

    public int ArchivosNuevos { get; set; }

    public int ArchivosModificados { get; set; }

    public int OmitidosPorFiltro { get; set; }

    public int YaSincronizados { get; set; }

    public int ErroresAcceso { get; set; }

    public long BytesPendientes { get; set; }

    public long BytesNuevos { get; set; }

    public long BytesModificados { get; set; }

    public long BytesOmitidosFiltro { get; set; }

    public IReadOnlyList<string> AvisosRiesgo { get; init; } = [];

    /// <summary>Archivos que se copiarían en una sincronización real.</summary>
    public int TotalPendientes => ArchivosNuevos + ArchivosModificados;
}

/// <summary>Agregado del análisis de todos los pares solicitados.</summary>
public sealed class ResultadoAnalisisGlobal
{
    public IReadOnlyList<ResultadoAnalisisPar> PorPar { get; init; } = [];

    public int TotalNuevos => PorPar.Sum(p => p.ArchivosNuevos);

    public int TotalModificados => PorPar.Sum(p => p.ArchivosModificados);

    public int TotalOmitidos => PorPar.Sum(p => p.OmitidosPorFiltro);

    public int TotalErrores => PorPar.Sum(p => p.ErroresAcceso);

    public long TotalBytesPendientes => PorPar.Sum(p => p.BytesPendientes);

    public long TotalBytesNuevos => PorPar.Sum(p => p.BytesNuevos);

    public long TotalBytesModificados => PorPar.Sum(p => p.BytesModificados);
}
