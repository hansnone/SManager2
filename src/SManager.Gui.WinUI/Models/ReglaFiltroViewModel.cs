namespace SManager.Gui.WinUI.Models;

/// <summary>Tipo de regla visual para filtros de exclusión.</summary>
public enum TipoReglaFiltro
{
    Extension,
    PatronAvanzado
}

/// <summary>Una regla de exclusión editable en la GUI.</summary>
public sealed class ReglaFiltroViewModel
{
    public TipoReglaFiltro Tipo { get; set; } = TipoReglaFiltro.Extension;

    public string Patron { get; set; } = "*.tmp";

    public string EtiquetaTipo => Tipo switch
    {
        TipoReglaFiltro.Extension => "Extensión",
        _ => "Patrón avanzado"
    };
}

/// <summary>Resultado de probar filtros sobre una carpeta de muestra.</summary>
public sealed class ResultadoPruebaFiltros
{
    public int ArchivosRevisados { get; init; }

    public int ArchivosCopiados { get; init; }

    public int ArchivosOmitidos { get; init; }
}
