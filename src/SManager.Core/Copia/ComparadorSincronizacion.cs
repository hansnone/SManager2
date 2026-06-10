using SManager.Core.Modelos;



namespace SManager.Core.Copia;



/// <summary>

/// Decide si un archivo del origen requiere copia comparando con el destino.

/// Misma lógica que usa el copiador al ejecutar el trabajo.

/// </summary>

public static class ComparadorSincronizacion

{

    private const double ToleranciaSegundosMtime = 2;



    /// <summary>Resuelve la ruta espejo en destino para un archivo del origen.</summary>

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



    /// <summary>Ruta del archivo respecto a la raíz del origen (clave del índice de destino).</summary>

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



            return origenNorm[raizOrigen.Length..].TrimStart('\\', '/');

        }

        catch

        {

            return null;

        }

    }



    /// <summary>true si falta en destino o difiere en tamaño/mtime respecto al origen.</summary>

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

            return DifiereDeDestino(infoOrigen.Length, infoOrigen.LastWriteTimeUtc, infoDestino.Length, infoDestino.LastWriteTimeUtc);

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



    internal static bool DifiereDeDestino(

        long tamanoOrigen,

        DateTime mtimeOrigenUtc,

        long tamanoDestino,

        DateTime mtimeDestinoUtc)

    {

        var diffSegundos = (mtimeOrigenUtc - mtimeDestinoUtc).TotalSeconds;

        return diffSegundos > ToleranciaSegundosMtime

            || (tamanoOrigen != tamanoDestino && Math.Abs(diffSegundos) <= ToleranciaSegundosMtime);

    }

}


