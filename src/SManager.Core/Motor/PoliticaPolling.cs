using SManager.Core.Modelos;

namespace SManager.Core.Motor;

/// <summary>Resuelve intervalos de polling global vs por par.</summary>
public static class PoliticaPolling
{
    public const int MinimoSegundos = 30;
    public const int MaximoSegundos = 3600;

    /// <summary>
    /// Intervalo efectivo del par: si no tiene valor propio (&lt;= 0 o null), usa el global.
    /// </summary>
    public static int ResolverIntervaloSegundos(ParSincronizacion par, ConfiguracionAplicacion config)
    {
        if (par.IntervaloPollingSegundos is int propio and > 0)
        {
            return Math.Clamp(propio, MinimoSegundos, MaximoSegundos);
        }

        return Math.Clamp(config.IntervaloPollingSegundos, MinimoSegundos, MaximoSegundos);
    }

    /// <summary>Normaliza el valor opcional del par (null o &lt;= 0 = heredar global).</summary>
    public static int? NormalizarIntervaloPar(int? segundos)
    {
        if (segundos is null or <= 0)
        {
            return null;
        }

        return Math.Clamp(segundos.Value, MinimoSegundos, MaximoSegundos);
    }
}
