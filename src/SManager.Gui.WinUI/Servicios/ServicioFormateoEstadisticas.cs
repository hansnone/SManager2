namespace SManager.Gui.WinUI.Servicios;

/// <summary>Formatea cantidades para el panel de estadísticas.</summary>
public static class ServicioFormateoEstadisticas
{
    /// <summary>Convierte bytes a B, KiB, MiB o GiB legibles.</summary>
    public static string FormatearBytes(long bytes)
    {
        if (bytes < 0)
        {
            bytes = 0;
        }

        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024L * 1024)
        {
            return $"{bytes / 1024.0:0.##} KiB";
        }

        if (bytes < 1024L * 1024 * 1024)
        {
            return $"{bytes / (1024.0 * 1024):0.##} MiB";
        }

        return $"{bytes / (1024.0 * 1024 * 1024):0.##} GiB";
    }

    /// <summary>Velocidad de transferencia a partir de bytes por segundo.</summary>
    public static string FormatearVelocidad(double bytesPorSegundo)
    {
        if (bytesPorSegundo <= 0)
        {
            return "—";
        }

        return $"{FormatearBytes((long)bytesPorSegundo)}/s";
    }

    /// <summary>Duración en formato hh:mm:ss o días si aplica.</summary>
    public static string FormatearDuracion(TimeSpan duracion)
    {
        if (duracion.TotalSeconds < 1)
        {
            return "0 s";
        }

        if (duracion.TotalDays >= 1)
        {
            return $"{(int)duracion.TotalDays} d {duracion.Hours:D2}:{duracion.Minutes:D2}:{duracion.Seconds:D2}";
        }

        return duracion.ToString(@"hh\:mm\:ss");
    }

    /// <summary>Porcentaje de CPU; 0 % es válido (demonio en reposo).</summary>
    public static string FormatearPorcentajeCpu(double valor) =>
        double.IsNaN(valor) ? "—" : $"{Math.Clamp(valor, 0, 100):0.#} %";

    /// <summary>Parsea ISO 8601 UTC y lo muestra en hora local.</summary>
    public static string FormatearInstanteUtc(string? instanteUtc)
    {
        if (string.IsNullOrWhiteSpace(instanteUtc))
        {
            return "—";
        }

        if (!DateTimeOffset.TryParse(instanteUtc, out var instante))
        {
            return "—";
        }

        return instante.ToLocalTime().ToString("g");
    }
}
