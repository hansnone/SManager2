namespace SManager.Core.Logging;

/// <summary>Escritor de log en disco con rotación por tamaño.</summary>
public sealed class EscritorLog : IAsyncDisposable
{
    private const long TamanoMaxBytes = 5 * 1024 * 1024;
    private readonly string _rutaLog;
    private readonly CancellationTokenSource _cts = new();
    private Task? _tarea;

    public EscritorLog(string rutaLog)
    {
        _rutaLog = rutaLog;
        var directorio = Path.GetDirectoryName(rutaLog);
        if (!string.IsNullOrEmpty(directorio))
        {
            Directory.CreateDirectory(directorio);
        }
    }

    public void Iniciar(Func<IReadOnlyList<string>> obtenerLineasPendientes)
    {
        _tarea = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var lineas = obtenerLineasPendientes();
                if (lineas.Count > 0)
                {
                    await EscribirLineasAsync(lineas).ConfigureAwait(false);
                }

                try
                {
                    await Task.Delay(200, _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, _cts.Token);
    }

    public async Task DetenerAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_tarea is not null)
        {
            await _tarea.ConfigureAwait(false);
        }
    }

    private async Task EscribirLineasAsync(IReadOnlyList<string> lineas)
    {
        try
        {
            RotarSiNecesario();
            var texto = string.Join(Environment.NewLine, lineas) + Environment.NewLine;
            await File.AppendAllTextAsync(_rutaLog, texto).ConfigureAwait(false);
        }
        catch
        {
            // El demonio no debe caer por fallos de log.
        }
    }

    private void RotarSiNecesario()
    {
        try
        {
            if (!File.Exists(_rutaLog))
            {
                return;
            }

            var info = new FileInfo(_rutaLog);
            if (info.Length <= TamanoMaxBytes)
            {
                return;
            }

            var respaldo = _rutaLog + ".1";
            if (File.Exists(respaldo))
            {
                File.Delete(respaldo);
            }

            File.Move(_rutaLog, respaldo);
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
