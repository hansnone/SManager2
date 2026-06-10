using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SManager.Core.Motor;

/// <summary>
/// Cola de copia con Channel y deduplicación por ruta normalizada.
/// Misma ruta no se encola dos veces hasta que un copiador la libere.
/// </summary>
public sealed class ColaTrabajosCopia
{
    public const int CapacidadPorDefecto = 50_000;

    private readonly Channel<TrabajoCopia> _canal;
    private readonly ConcurrentDictionary<string, byte> _rutasPendientes =
        new(StringComparer.OrdinalIgnoreCase);

    public ColaTrabajosCopia(int capacidad = CapacidadPorDefecto)
    {
        _canal = Channel.CreateBounded<TrabajoCopia>(new BoundedChannelOptions(capacidad)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
    }

    public int PendientesEnCola => _rutasPendientes.Count;

    public ResultadoEncoladoCopia IntentarEncolar(TrabajoCopia trabajo, MetricasMotor metricas)
    {
        var clave = NormalizarRuta(trabajo.RutaCompleta);
        if (!_rutasPendientes.TryAdd(clave, 0))
        {
            metricas.IncrementarDuplicadosEvitados();
            return ResultadoEncoladoCopia.DuplicadoEnCola;
        }

        if (!_canal.Writer.TryWrite(trabajo with { RutaCompleta = clave }))
        {
            _rutasPendientes.TryRemove(clave, out _);
            return ResultadoEncoladoCopia.ColaLlena;
        }

        return ResultadoEncoladoCopia.Encolado;
    }

    public async ValueTask<TrabajoCopia> LeerAsync(CancellationToken cancelacion)
    {
        return await _canal.Reader.ReadAsync(cancelacion).ConfigureAwait(false);
    }

    public bool IntentarLeer(out TrabajoCopia? trabajo) =>
        _canal.Reader.TryRead(out trabajo);

    public void LiberarRuta(string rutaCompleta)
    {
        _rutasPendientes.TryRemove(NormalizarRuta(rutaCompleta), out _);
    }

    public async Task EsperarVaciadoAsync(TimeSpan timeout, CancellationToken cancelacion)
    {
        var limite = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < limite && !cancelacion.IsCancellationRequested)
        {
            if (_rutasPendientes.IsEmpty && !_canal.Reader.TryPeek(out _))
            {
                return;
            }

            await Task.Delay(250, cancelacion).ConfigureAwait(false);
        }
    }

    public void CompletarEscritura() => _canal.Writer.TryComplete();

    private static string NormalizarRuta(string ruta)
    {
        try
        {
            return Path.GetFullPath(ruta);
        }
        catch
        {
            return ruta;
        }
    }
}
