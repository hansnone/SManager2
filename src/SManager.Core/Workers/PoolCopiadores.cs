using SManager.Core.Copia;
using SManager.Core.Modelos;
using SManager.Core.Motor;

namespace SManager.Core.Workers;

/// <summary>Pool central de copiadores que consume la ColaCopia.</summary>
public sealed class PoolCopiadores : IAsyncDisposable
{
    private readonly EstadoMotor _estado;
    private readonly ServicioCopia _servicioCopia = new();
    private readonly List<Task> _tareas = [];
    private readonly CancellationTokenSource _cts = new();

    public PoolCopiadores(EstadoMotor estado)
    {
        _estado = estado;
    }

    public void Iniciar(int cantidad)
    {
        _estado.SolicitudParadaCopiadores = false;
        for (var i = 1; i <= cantidad; i++)
        {
            var idCopiador = i;
            _tareas.Add(Task.Run(() => EjecutarCopiadorAsync(idCopiador, _cts.Token), _cts.Token));
        }
    }

    private async Task EjecutarCopiadorAsync(int idCopiador, CancellationToken cancelacion)
    {
        while (!_estado.SolicitudParadaCopiadores && !cancelacion.IsCancellationRequested)
        {
            TrabajoCopia trabajo;
            try
            {
                trabajo = await _estado.ColaCopia.LeerAsync(cancelacion).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var par = ObtenerPar(trabajo.IdPar);
            if (par is null)
            {
                _estado.ColaCopia.LiberarRuta(trabajo.RutaCompleta);
                continue;
            }

            var nombreArchivo = Path.GetFileName(trabajo.RutaCompleta);
            var rutaRelativa = nombreArchivo;
            try
            {
                var raiz = Path.GetFullPath(par.RutaOrigen.Trim());
                if (trabajo.RutaCompleta.StartsWith(raiz, StringComparison.OrdinalIgnoreCase))
                {
                    rutaRelativa = trabajo.RutaCompleta[raiz.Length..].TrimStart('\\', '/');
                }
            }
            catch
            {
                // Mantener nombre simple.
            }

            _estado.CopiasEnCurso[trabajo.RutaCompleta] = new CopiaEnCursoInterna(
                rutaRelativa, par.IdPar, idCopiador);

            try
            {
                _servicioCopia.EjecutarCopiaCondicional(_estado, trabajo.RutaCompleta, par, idCopiador);
            }
            catch (Exception ex)
            {
                _estado.EncolarLog(trabajo.IdPar, "ERROR", $"Copiador #{idCopiador}: {ex.Message}");
            }
            finally
            {
                _estado.CopiasEnCurso.TryRemove(trabajo.RutaCompleta, out _);
                _estado.ColaCopia.LiberarRuta(trabajo.RutaCompleta);
            }
        }
    }

    private ParSincronizacion? ObtenerPar(string idPar)
    {
        lock (_estado.CandadoPares)
        {
            return _estado.Pares.FirstOrDefault(p => p.IdPar == idPar);
        }
    }

    public async Task DetenerAsync(TimeSpan timeoutDrenado)
    {
        _estado.SolicitudParadaCopiadores = true;
        _estado.ColaCopia.CompletarEscritura();

        var limite = DateTime.UtcNow.Add(timeoutDrenado);
        while (DateTime.UtcNow < limite)
        {
            if (_estado.ColaCopia.PendientesEnCola == 0 && _estado.CopiasEnCurso.IsEmpty)
            {
                break;
            }

            await Task.Delay(250).ConfigureAwait(false);
        }

        await _cts.CancelAsync().ConfigureAwait(false);
        try
        {
            await Task.WhenAll(_tareas).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort en apagado.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DetenerAsync(TimeSpan.FromMinutes(10)).ConfigureAwait(false);
        _cts.Dispose();
    }
}
