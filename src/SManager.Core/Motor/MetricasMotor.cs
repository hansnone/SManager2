namespace SManager.Core.Motor;

/// <summary>Contadores globales thread-safe del motor.</summary>
public sealed class MetricasMotor
{
    private int _totalCopiados;
    private int _totalErrores;
    private int _duplicadosEvitados;
    private long _bytesEscritos;

    public int TotalCopiados => Volatile.Read(ref _totalCopiados);
    public int TotalErrores => Volatile.Read(ref _totalErrores);
    public int DuplicadosEvitados => Volatile.Read(ref _duplicadosEvitados);
    public long BytesEscritos => Volatile.Read(ref _bytesEscritos);

    public void IncrementarCopiados() => Interlocked.Increment(ref _totalCopiados);

    public void IncrementarErrores() => Interlocked.Increment(ref _totalErrores);

    public void IncrementarDuplicadosEvitados() => Interlocked.Increment(ref _duplicadosEvitados);

    public void SumarBytesEscritos(long bytes) =>
        Interlocked.Add(ref _bytesEscritos, bytes);
}
