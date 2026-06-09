namespace SManager.Gui.WinUI.Models;

using SManager.Gui.WinUI.Servicios;

/// <summary>Fila de estadísticas por par en el panel de métricas.</summary>
public sealed class EstadisticaParItemViewModel
{
    public string Nombre { get; init; } = string.Empty;

    public string Estado { get; init; } = string.Empty;

    public int Copiados { get; init; }

    public int Errores { get; init; }

    public string UltimaSincronizacion { get; init; } = "—";

    public static EstadisticaParItemViewModel DesdeResumen(
        string nombre,
        string estado,
        int copiados,
        int errores,
        string? ultimaSincronizacionUtc) =>
        new()
        {
            Nombre = nombre,
            Estado = estado,
            Copiados = copiados,
            Errores = errores,
            UltimaSincronizacion = ServicioFormateoEstadisticas.FormatearInstanteUtc(ultimaSincronizacionUtc)
        };
}
