using SManager.Core.Copia;
using SManager.Core.Motor;

namespace SManager.Core.Workers;

/// <summary>Pool de hidratadores OneDrive.</summary>
public sealed class PoolHidratadores : IAsyncDisposable
{
    private readonly EstadoMotor _estado;
    private readonly ServicioHidratacion _servicio = new();
    private readonly List<Task> _tareas = [];
    private readonly CancellationTokenSource _cts = new();

    public PoolHidratadores(EstadoMotor estado)
    {
        _estado = estado;
    }

    public void Iniciar(int cantidad)
    {
        _estado.SolicitudParadaHidratadores = false;
        for (var i = 1; i <= cantidad; i++)
        {
            var id = i;
            _tareas.Add(Task.Run(() => EjecutarHidratadorAsync(id, _cts.Token), _cts.Token));
        }
    }

    private async Task EjecutarHidratadorAsync(int idHidratador, CancellationToken cancelacion)
    {
        while (!_estado.SolicitudParadaHidratadores && !cancelacion.IsCancellationRequested)
        {
            TrabajoHidratacion trabajo;
            try
            {
                trabajo = await _estado.ColaHidratacion.LeerAsync(cancelacion).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await _servicio.ProcesarTrabajoAsync(_estado, trabajo, idHidratador, cancelacion)
                .ConfigureAwait(false);
        }
    }

    public async Task DetenerAsync()
    {
        _estado.SolicitudParadaHidratadores = true;
        _estado.ColaHidratacion.CompletarEscritura();
        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
            await Task.WhenAll(_tareas).WaitAsync(TimeSpan.FromSeconds(8)).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DetenerAsync().ConfigureAwait(false);
        _cts.Dispose();
    }
}
