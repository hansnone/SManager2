namespace SManager.Gui.WinUI.Models;

/// <summary>Plantilla preconfigurada para el asistente de primer par.</summary>
public sealed class PlantillaParEjemplo
{
    public string Id { get; init; } = string.Empty;

    public string Titulo { get; init; } = string.Empty;

    public string Descripcion { get; init; } = string.Empty;

    public string NombreParSugerido { get; init; } = "Mi par";

    public string FiltroInclusion { get; init; } = "*";

    public string FiltroExclusion { get; init; } = "~$*;*.tmp;*.partial;*.lnk";

    /// <summary>Texto orientativo para el campo destino (no se aplica automáticamente).</summary>
    public string? PistaDestino { get; init; }
}
