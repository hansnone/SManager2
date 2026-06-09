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

    public ConcurrentDictionary<string, bool> ParadasVigiaPorId { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public DateTime? ProximoPollingUtc { get; set; }

    /// <summary>Momento UTC en que arrancó esta sesión del demonio.</summary>
    public DateTime InicioSesionUtc { get; set; }

    public MetricasMotor Metricas { get; } = new();

    public readonly object CandadoPares = new();

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
