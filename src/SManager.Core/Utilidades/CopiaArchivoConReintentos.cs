namespace SManager.Core.Utilidades;

/// <summary>
/// Copia archivos tolerando bloqueos transitorios de Windows (GUI guardando, antivirus, etc.).
/// </summary>
public static class CopiaArchivoConReintentos
{
    private const int MaxReintentos = 3;
    private static readonly TimeSpan EsperaEntreIntentos = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Copia origen → destino con reintentos ante IOException o UnauthorizedAccessException.
    /// </summary>
    public static void Copiar(string rutaOrigen, string rutaDestino, bool sobrescribir = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaOrigen);
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaDestino);

        var directorioDestino = Path.GetDirectoryName(rutaDestino);
        if (!string.IsNullOrEmpty(directorioDestino))
        {
            Directory.CreateDirectory(directorioDestino);
        }

        Exception? ultimoError = null;
        for (var intento = 1; intento <= MaxReintentos; intento++)
        {
            try
            {
                File.Copy(rutaOrigen, rutaDestino, sobrescribir);
                return;
            }
            catch (IOException ex)
            {
                ultimoError = ex;
            }
            catch (UnauthorizedAccessException ex)
            {
                ultimoError = ex;
            }

            if (intento < MaxReintentos)
            {
                Thread.Sleep(EsperaEntreIntentos);
            }
        }

        throw new IOException(
            $"No se pudo copiar '{rutaOrigen}' → '{rutaDestino}' tras {MaxReintentos} intentos.",
            ultimoError);
    }

    /// <summary>Versión async que delega en el hilo del pool (evita bloquear el bucle del demonio).</summary>
    public static Task CopiarAsync(
        string rutaOrigen,
        string rutaDestino,
        bool sobrescribir = true,
        CancellationToken cancelacion = default) =>
        Task.Run(() => Copiar(rutaOrigen, rutaDestino, sobrescribir), cancelacion);
}
