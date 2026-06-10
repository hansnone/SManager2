using SManager.Core.Modelos;

namespace SManager.Core.Copia;

/// <summary>
/// Índice en memoria del árbol de destino (ruta relativa → tamaño/mtime).
/// Se construye una vez por escaneo para evitar N consultas a red/disco.
/// </summary>
public sealed class IndiceMetadatosDestino
{
    private readonly Dictionary<string, MetadatosArchivoDestino> _porRutaRelativa;

    private IndiceMetadatosDestino(Dictionary<string, MetadatosArchivoDestino> porRutaRelativa)
    {
        _porRutaRelativa = porRutaRelativa;
    }

    public int CantidadArchivos => _porRutaRelativa.Count;

    /// <summary>Recorre el destino una vez y carga metadatos en un diccionario.</summary>
    public static IndiceMetadatosDestino Construir(ParSincronizacion par, CancellationToken cancelacion)
    {
        var indice = new Dictionary<string, MetadatosArchivoDestino>(StringComparer.OrdinalIgnoreCase);

        string raizDestino;
        try
        {
            raizDestino = Path.GetFullPath(
                par.RutaDestino.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
        catch
        {
            return new IndiceMetadatosDestino(indice);
        }

        if (!Directory.Exists(raizDestino))
        {
            return new IndiceMetadatosDestino(indice);
        }

        var prefijoLongitud = raizDestino.Length;

        foreach (var rutaAbsoluta in Directory.EnumerateFiles(raizDestino, "*", SearchOption.AllDirectories))
        {
            cancelacion.ThrowIfCancellationRequested();

            try
            {
                var relativa = NormalizarRutaRelativa(rutaAbsoluta, prefijoLongitud);
                if (string.IsNullOrEmpty(relativa))
                {
                    continue;
                }

                var info = new FileInfo(rutaAbsoluta);
                if (!info.Exists || info.Attributes.HasFlag(FileAttributes.Directory))
                {
                    continue;
                }

                indice[relativa] = new MetadatosArchivoDestino(info.Length, info.LastWriteTimeUtc);
            }
            catch
            {
                // Archivo inaccesible durante el barrido: se tratará como desconocido en la comparación.
            }
        }

        return new IndiceMetadatosDestino(indice);
    }

    /// <summary>Busca metadatos por ruta relativa respecto a la raíz del destino.</summary>
    public bool TryObtener(string rutaRelativa, out MetadatosArchivoDestino metadatos)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa))
        {
            metadatos = default;
            return false;
        }

        var clave = rutaRelativa.TrimStart('\\', '/');
        return _porRutaRelativa.TryGetValue(clave, out metadatos);
    }

    private static string NormalizarRutaRelativa(string rutaAbsoluta, int longitudRaiz)
    {
        if (rutaAbsoluta.Length <= longitudRaiz)
        {
            return string.Empty;
        }

        return rutaAbsoluta[longitudRaiz..].TrimStart('\\', '/');
    }
}
