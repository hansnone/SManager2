using SManager.Core.Motor;

namespace SManager.Core.Vigia;

/// <summary>Arranca y detiene un vigía por cada par habilitado.</summary>
public sealed class GestorVigias : IAsyncDisposable
{
    private readonly EstadoMotor _estado;
    private readonly Dictionary<string, VigiaPar> _vigiasPorId = new(StringComparer.OrdinalIgnoreCase);

    public GestorVigias(EstadoMotor estado)
    {
        _estado = estado;
    }

    public void IniciarTodos()
    {
        lock (_estado.CandadoPares)
        {
            foreach (var par in _estado.Pares.Where(p => p.Habilitado))
            {
                if (_vigiasPorId.ContainsKey(par.IdPar))
                {
                    continue;
                }

                var vigia = new VigiaPar(_estado, par.IdPar);
                vigia.Iniciar();
                _vigiasPorId[par.IdPar] = vigia;
            }
        }
    }

    public async Task DetenerTodosAsync()
    {
        _estado.SolicitudParada = true;
        foreach (var vigia in _vigiasPorId.Values.ToList())
        {
            await vigia.DetenerAsync().ConfigureAwait(false);
            await vigia.DisposeAsync().ConfigureAwait(false);
        }

        _vigiasPorId.Clear();
    }

    public async Task ReiniciarVigiaAsync(string idPar)
    {
        await DetenerVigiaAsync(idPar).ConfigureAwait(false);

        lock (_estado.CandadoPares)
        {
            var par = _estado.Pares.FirstOrDefault(p => p.IdPar == idPar && p.Habilitado);
            if (par is null)
            {
                return;
            }

            var vigia = new VigiaPar(_estado, idPar);
            vigia.Iniciar();
            _vigiasPorId[idPar] = vigia;
        }
    }

    public async Task DetenerVigiaAsync(string idPar)
    {
        _estado.ParadasVigiaPorId[idPar] = true;
        if (_vigiasPorId.TryGetValue(idPar, out var vigia))
        {
            await vigia.DetenerAsync().ConfigureAwait(false);
            await vigia.DisposeAsync().ConfigureAwait(false);
            _vigiasPorId.Remove(idPar);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DetenerTodosAsync().ConfigureAwait(false);
    }
}
