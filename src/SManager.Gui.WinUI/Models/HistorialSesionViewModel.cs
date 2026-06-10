using SManager.Gui.WinUI.Servicios;

namespace SManager.Gui.WinUI.Models;

/// <summary>Fila del historial de sesiones mostrada en Estadísticas.</summary>
public sealed class HistorialSesionViewModel
{
    public required string TextoInicio { get; init; }

    public required string TextoDuracion { get; init; }

    public required string TextoResumen { get; init; }

    public required string TextoEstado { get; init; }

    public required string ClaveEstado { get; init; }

    public static HistorialSesionViewModel DesdeDto(EntradaHistorialSesionDto dto)
    {
        DateTimeOffset.TryParse(dto.InicioUtc, out var inicio);
        DateTimeOffset.TryParse(dto.FinUtc, out var fin);
        var duracion = fin > inicio ? fin - inicio : TimeSpan.Zero;
        var exito = dto.Errores == 0;

        return new HistorialSesionViewModel
        {
            TextoInicio = ServicioFormateoEstadisticas.FormatearInstanteUtc(dto.InicioUtc),
            TextoDuracion = duracion > TimeSpan.Zero
                ? ServicioFormateoEstadisticas.FormatearDuracion(duracion)
                : "—",
            TextoResumen =
                $"{dto.Copiados:N0} copiados · {ServicioFormateoEstadisticas.FormatearBytes(dto.BytesEscritos)} · {dto.Errores:N0} errores",
            TextoEstado = exito ? "Correcta" : "Con errores",
            ClaveEstado = exito ? "OK" : "ERROR"
        };
    }
}
