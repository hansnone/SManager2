namespace SManager.Gui.WinUI.Models;

public sealed class ActividadViewModel
{
    public string Hora { get; init; } = string.Empty;
    public string Tipo { get; init; } = string.Empty;
    public string Archivo { get; init; } = string.Empty;

    /// <summary>Identificador interno del par (telemetría IPC).</summary>
    public string IdPar { get; init; } = string.Empty;

    /// <summary>Nombre legible del par para la columna «Par» del monitor.</summary>
    public string NombrePar { get; init; } = string.Empty;
}
