using System.Collections.Concurrent;
using SManager.Core.Modelos;

namespace SManager.Core.Motor;

/// <summary>Estado compartido entre vigías, copiadores, hidratadores y el orquestador.</summary>
public sealed class EstadoMotor
{
    public required ConfiguracionAplicacion Config { get; set; }
    public required List<ParSincronizacion> Pares { get; set; }

    public volatile bool AceptarNuevosTrabajos = true;
    public volatile bool SolicitudParada;
    public volatile bool SolicitudParadaCopiadores;
    public volatile bool SolicitudParadaHidratadores;
    public volatile bool EnEjecucion;
    /// <summary>
    /// Indica si el usuario se ha autenticado conscientemente como administrador local de Windows en esta sesión.
    /// Inicia siempre en false tras cada arranque de la aplicación.
    /// </summary>
    public volatile bool SesionBorradoDesbloqueada;

    public ColaTrabajosCopia ColaCopia { get; } = new();
    public ColaTrabajosHidratacion ColaHidratacion { get; } = new();

    public ConcurrentDictionary<string, byte> HidratacionesActivas { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public ConcurrentQueue<string> ColaLogs { get; } = new();
    public ConcurrentQueue<EstadisticaPar> ColaEstadisticas { get; } = new();
    public ConcurrentQueue<string> ColaDisco { get; } = new();

    public ConcurrentDictionary<string, CopiaEnCursoInterna> CopiasEnCurso { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public ConcurrentQueue<EntradaActividadInterna> ColaActividad { get; } = new();
    public List<EntradaActividadInterna> HistorialActividad { get; } = [];
    public object CandadoHistorial { get; } = new();

    public ConcurrentDictionary<string, bool> PeticionEscaneoCompleto { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>True si la petición de escaneo proviene del temporizador de polling (no arranque/recarga).</summary>
    public ConcurrentDictionary<string, bool> PeticionEscaneoPorPolling { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Pares con purga masiva en Modo Espejo pausada por seguridad -> Conteo de archivos pendientes de purga.</summary>
    public ConcurrentDictionary<string, int> PurgasMasivasBloqueadas { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Autorizaciones conscientes del usuario para purga masiva intencionada de una única pasada.</summary>
    public ConcurrentDictionary<string, bool> AutorizacionPurgaMasivaUnaVez { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>True mientras el vigía ejecuta un escaneo completo del par.</summary>
    public ConcurrentDictionary<string, bool> EscaneoEnCursoPorPar { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Polling diferido: un barrido llegó mientras el escaneo anterior seguía activo.</summary>
    public ConcurrentDictionary<string, bool> EscaneoPollingPendientePorPar { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Próximo barrido de seguridad programado por par (UTC).</summary>
    public ConcurrentDictionary<string, DateTime> ProximoPollingPorParUtc { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public ConcurrentDictionary<string, bool> ParadasVigiaPorId { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Momento UTC en que arrancó esta sesión del demonio.</summary>
    public DateTime InicioSesionUtc { get; set; }

    public MetricasMotor Metricas { get; } = new();

    public readonly object CandadoPares = new();

    /// <summary>Programa el temporizador de polling de un par desde ahora.</summary>
    public void ProgramarProximoPolling(ParSincronizacion par)
    {
        var segundos = PoliticaPolling.ResolverIntervaloSegundos(par, Config);
        ProximoPollingPorParUtc[par.IdPar] = DateTime.UtcNow.AddSeconds(segundos);
    }

    /// <summary>
    /// Solicita escaneo por polling: inmediato si no hay escaneo en curso;
    /// si no, encola un único barrido pendiente (coalescencia).
    /// </summary>
    public void SolicitarEscaneoPorPolling(string idPar)
    {
        if (EscaneoEnCursoPorPar.TryGetValue(idPar, out var enCurso) && enCurso)
        {
            EscaneoPollingPendientePorPar[idPar] = true;
            EncolarLog(idPar, "INFO", "Polling de seguridad encolado (escaneo en curso)");
            return;
        }

        PeticionEscaneoCompleto[idPar] = true;
        PeticionEscaneoPorPolling[idPar] = true;
    }

    /// <summary>Segundos hasta el próximo polling entre todos los pares activos (mínimo).</summary>
    public int? SegundosHastaProximoPollingGlobal()
    {
        if (ProximoPollingPorParUtc.IsEmpty)
        {
            return null;
        }

        var ahora = DateTime.UtcNow;
        var minimo = ProximoPollingPorParUtc.Values
            .Select(proximo => (int)Math.Max(0, (proximo - ahora).TotalSeconds))
            .DefaultIfEmpty(0)
            .Min();

        return minimo;
    }

    /// <summary>Segundos hasta el próximo polling de un par concreto.</summary>
    public int? SegundosHastaProximoPolling(string idPar)
    {
        if (!ProximoPollingPorParUtc.TryGetValue(idPar, out var proximo))
        {
            return null;
        }

        return (int)Math.Max(0, (proximo - DateTime.UtcNow).TotalSeconds);
    }

    public void EncolarLog(string idPar, string nivel, string mensaje)
    {
        ColaLogs.Enqueue($"{idPar}|{nivel}|{mensaje}");
    }

    public void RegistrarActividad(string tipo, string archivo, string idPar, string? detalle = null)
    {
        var entrada = new EntradaActividadInterna(
            DateTime.Now.ToString("HH:mm:ss"),
            tipo,
            archivo,
            idPar,
            detalle);

        ColaActividad.Enqueue(entrada);
    }

    public void DrenarActividadAlHistorial(int maxEntradas = 50)
    {
        while (ColaActividad.TryDequeue(out var entrada))
        {
            lock (CandadoHistorial)
            {
                HistorialActividad.Add(entrada);
                while (HistorialActividad.Count > maxEntradas)
                {
                    HistorialActividad.RemoveAt(0);
                }
            }
        }
    }
}
