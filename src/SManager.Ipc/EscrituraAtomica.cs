namespace SManager.Ipc;

/// <summary>
/// Escritura atómica .tmp + rename con reintentos.
/// Evita que lectores concurrentes (GUI, CLI status) vean JSON a medias.
/// </summary>
public static class EscrituraAtomica
{
    private const int MaxReintentos = 5;
    private static readonly TimeSpan EsperaReintento = TimeSpan.FromMilliseconds(10);

    public static async Task EscribirTextoAsync(string rutaDestino, string contenido, CancellationToken cancelacion = default)
    {
        var rutaTmp = rutaDestino + ".tmp";
        await File.WriteAllTextAsync(rutaTmp, contenido, cancelacion).ConfigureAwait(false);

        for (var intento = 0; intento < MaxReintentos; intento++)
        {
            cancelacion.ThrowIfCancellationRequested();
            try
            {
                File.Move(rutaTmp, rutaDestino, overwrite: true);
                return;
            }
            catch (IOException) when (intento < MaxReintentos - 1)
            {
                await Task.Delay(EsperaReintento, cancelacion).ConfigureAwait(false);
            }
        }

        try { File.Delete(rutaTmp); } catch { /* limpieza best-effort */ }
        throw new IOException($"No se pudo publicar el archivo de forma atómica: {rutaDestino}");
    }

    public static async Task<string?> LeerTextoConReintentoAsync(string ruta, CancellationToken cancelacion = default)
    {
        if (!File.Exists(ruta))
        {
            return null;
        }

        for (var intento = 0; intento < MaxReintentos; intento++)
        {
            cancelacion.ThrowIfCancellationRequested();
            try
            {
                return await File.ReadAllTextAsync(ruta, cancelacion).ConfigureAwait(false);
            }
            catch (IOException) when (intento < MaxReintentos - 1)
            {
                await Task.Delay(EsperaReintento, cancelacion).ConfigureAwait(false);
            }
        }

        return null;
    }
}
