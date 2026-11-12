using SManager.Core.Modelos;

namespace SManager.Core.Copia;

/// <summary>
/// Decide si un archivo del origen requiere copia comparando su tamaño y fecha con el destino.
/// Es utilizada tanto por el motor en vivo como por el simulador de «Analizar cambios».
/// </summary>
public static class ComparadorSincronizacion
{
    // Margen de 2 segundos para absorber diferencias de precisión entre sistemas de archivos (NTFS, FAT32, exFAT, SMB).
    private const double ToleranciaSegundosMtime = 2;

    /// <summary>Resuelve la ruta equivalente en la carpeta destino para un archivo del origen.</summary>
    public static string? ObtenerRutaDestino(ParSincronizacion par, string rutaOrigenCompleta)
    {
        var relativa = ObtenerRutaRelativa(par, rutaOrigenCompleta);
        if (relativa is null)
        {
            return null;
        }

        try
        {
            var raizDestino = Path.GetFullPath(
                par.RutaDestino.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return Path.Combine(raizDestino, relativa);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Calcula la ruta relativa del archivo respecto a la carpeta raíz de origen.
    /// Comprueba el límite de separador de directorio para evitar falsos prefijos (ej. C:\Fotos vs C:\Fotos2).
    /// </summary>
    public static string? ObtenerRutaRelativa(ParSincronizacion par, string rutaOrigenCompleta)
    {
        try
        {
            var raizOrigen = Path.GetFullPath(
                par.RutaOrigen.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var origenNorm = Path.GetFullPath(rutaOrigenCompleta);

            if (!origenNorm.StartsWith(raizOrigen, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Verificamos que justo después de la raíz haya un separador de carpeta.
            if (origenNorm.Length <= raizOrigen.Length)
            {
                return null;
            }

            var caracterSiguiente = origenNorm[raizOrigen.Length];
            if (caracterSiguiente != Path.DirectorySeparatorChar && caracterSiguiente != Path.AltDirectorySeparatorChar)
            {
                return null;
            }

            return origenNorm[(raizOrigen.Length + 1)..].TrimStart('\\', '/');
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Devuelve true si el archivo no existe en destino o si difiere en tamaño o fecha de modificación.</summary>
    public static bool NecesitaCopia(
        FileInfo infoOrigen,
        ParSincronizacion par,
        IndiceMetadatosDestino? indiceDestino = null)
    {
        if (indiceDestino is not null)
        {
            return NecesitaCopiaConIndice(infoOrigen, par, indiceDestino);
        }

        var rutaDestino = ObtenerRutaDestino(par, infoOrigen.FullName);
        if (string.IsNullOrEmpty(rutaDestino))
        {
            return true;
        }

        if (!File.Exists(rutaDestino))
        {
            return true;
        }

        try
        {
            var infoDestino = new FileInfo(rutaDestino);
            return DifiereDeDestino(
                infoOrigen.Length,
                infoOrigen.LastWriteTimeUtc,
                infoDestino.Length,
                infoDestino.LastWriteTimeUtc);
        }
        catch
        {
            return true;
        }
    }

    private static bool NecesitaCopiaConIndice(
        FileInfo infoOrigen,
        ParSincronizacion par,
        IndiceMetadatosDestino indiceDestino)
    {
        var relativa = ObtenerRutaRelativa(par, infoOrigen.FullName);
        if (relativa is null)
        {
            return true;
        }

        if (!indiceDestino.TryObtener(relativa, out var metadatos))
        {
            return true;
        }

        return DifiereDeDestino(
            infoOrigen.Length,
            infoOrigen.LastWriteTimeUtc,
            metadatos.Tamano,
            metadatos.MtimeUtc);
    }

    /// <summary>
    /// Determina si hay diferencias entre origen y destino:
    /// 1) Si el tamaño en bytes es distinto, siempre necesita copia.
    /// 2) Si el archivo de origen es más reciente que el de destino (superando la tolerancia de 2 s), necesita copia.
    /// </summary>
    internal static bool DifiereDeDestino(
        long tamanoOrigen,
        DateTime mtimeOrigenUtc,
        long tamanoDestino,
        DateTime mtimeDestinoUtc)
    {
        if (tamanoOrigen != tamanoDestino)
        {
            return true;
        }

        var diffSegundos = (mtimeOrigenUtc - mtimeDestinoUtc).TotalSeconds;
        return diffSegundos > ToleranciaSegundosMtime;
    }
}
