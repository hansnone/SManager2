namespace SManager.Gui.WinUI.Servicios;

/// <summary>Calcula el tamaño total de una carpeta en disco (recursivo, tolerante a errores).</summary>
public sealed class ServicioTamanoDirectorio
{
    /// <summary>Suma bytes de todos los archivos accesibles bajo <paramref name="ruta"/>.</summary>
    public Task<long> CalcularAsync(string ruta, CancellationToken cancelacion = default) =>
        Task.Run(() => Calcular(ruta, cancelacion), cancelacion);

    private static long Calcular(string ruta, CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(ruta) || !Directory.Exists(ruta))
        {
            return 0;
        }

        long total = 0;
        var pendientes = new Stack<string>();
        pendientes.Push(ruta);

        while (pendientes.Count > 0)
        {
            cancelacion.ThrowIfCancellationRequested();

            var actual = pendientes.Pop();

            try
            {
                foreach (var archivo in Directory.EnumerateFiles(actual))
                {
                    cancelacion.ThrowIfCancellationRequested();

                    try
                    {
                        total += new FileInfo(archivo).Length;
                    }
                    catch
                    {
                        // Archivo bloqueado o sin permisos: omitir.
                    }
                }

                foreach (var directorio in Directory.EnumerateDirectories(actual))
                {
                    pendientes.Push(directorio);
                }
            }
            catch
            {
                // Carpeta inaccesible: omitir rama.
            }
        }

        return total;
    }
}
