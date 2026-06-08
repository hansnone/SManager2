using System.Threading.Channels;

namespace SManager.Core.Motor;

/// <summary>Cola de hidratación OneDrive basada en Channel.</summary>
public sealed class ColaTrabajosHidratacion
{
    private readonly Channel<TrabajoHidratacion> _canal;

    public ColaTrabajosHidratacion(int capacidad = 2_000)
    {
        _canal = Channel.CreateBounded<TrabajoHidratacion>(new BoundedChannelOptions(capacidad)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
    }

    public async ValueTask EncolarAsync(TrabajoHidratacion trabajo, CancellationToken cancelacion) =>
        await _canal.Writer.WriteAsync(trabajo, cancelacion).ConfigureAwait(false);

    public bool IntentarEncolar(TrabajoHidratacion trabajo) =>
        _canal.Writer.TryWrite(trabajo);

    public async ValueTask<TrabajoHidratacion> LeerAsync(CancellationToken cancelacion) =>
        await _canal.Reader.ReadAsync(cancelacion).ConfigureAwait(false);

    public bool IntentarLeer(out TrabajoHidratacion? trabajo) =>
        _canal.Reader.TryRead(out trabajo);

    public void CompletarEscritura() => _canal.Writer.TryComplete();
}
