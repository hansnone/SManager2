using System.IO.Enumeration;
using SManager.Core.Modelos;

namespace SManager.Core.Utilidades;

/// <summary>
/// Evalúa si un archivo debe sincronizarse o ignorarse según los patrones de inclusión y exclusión.
/// Admite comodines glob (* y ?) tanto sobre el nombre del archivo como sobre las carpetas de su ruta relativa.
/// </summary>
public static class ServicioFiltros
{
    /// <summary>
    /// Comprueba si un archivo supera las reglas de inclusión y no cae en ninguna regla de exclusión.
    /// </summary>
    /// <param name="nombreArchivo">Nombre corto del archivo (ej. "informe.pdf").</param>
    /// <param name="par">Configuración del par con sus cadenas FiltroInclusion y FiltroExclusion.</param>
    /// <param name="rutaRelativa">Ruta relativa opcional respecto al origen (ej. "node_modules/pkg/index.js") para filtrar carpetas.</param>
    public static bool PasaFiltros(string nombreArchivo, ParSincronizacion par, string? rutaRelativa = null)
    {
        var inclusiones = DividirPatrones(par.FiltroInclusion);
        var exclusiones = DividirPatrones(par.FiltroExclusion);

        // 1. Si hay reglas de inclusión definidas, el archivo debe coincidir al menos con una.
        if (inclusiones.Count > 0 && !CoincideAlguno(nombreArchivo, inclusiones, rutaRelativa))
        {
            return false;
        }

        // 2. Si coincide con alguna regla de exclusión (en el nombre o en una subcarpeta como node_modules), se omite.
        if (exclusiones.Count > 0 && CoincideAlguno(nombreArchivo, exclusiones, rutaRelativa))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Divide una cadena de patrones separados por punto y coma (;) o coma (,) en una lista limpia.
    /// </summary>
    public static IReadOnlyList<string> DividirPatrones(string? cadena)
    {
        if (string.IsNullOrWhiteSpace(cadena))
        {
            return [];
        }

        return cadena.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Verifica si el nombre del archivo o alguno de los segmentos de su ruta relativa coincide con la lista de patrones.
    /// </summary>
    private static bool CoincideAlguno(string nombreArchivo, IReadOnlyList<string> patrones, string? rutaRelativa)
    {
        // Si disponemos de ruta relativa, separamos sus carpetas para poder excluir directorios completos (ej. "node_modules" o ".git").
        string[]? segmentosRuta = null;
        if (!string.IsNullOrWhiteSpace(rutaRelativa))
        {
            segmentosRuta = rutaRelativa.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        }

        foreach (var patronCrudo in patrones)
        {
            if (string.IsNullOrWhiteSpace(patronCrudo))
            {
                continue;
            }

            var patron = patronCrudo.Trim();

            // Coincidencia directa con el nombre del archivo (ej. "*.tmp" o "~$*").
            if (FileSystemName.MatchesSimpleExpression(patron, nombreArchivo, ignoreCase: true))
            {
                return true;
            }

            // Si hay ruta relativa, comprobamos cada carpeta contenedora y la ruta completa.
            if (segmentosRuta is not null)
            {
                foreach (var segmento in segmentosRuta)
                {
                    if (FileSystemName.MatchesSimpleExpression(patron, segmento, ignoreCase: true))
                    {
                        return true;
                    }
                }

                if (FileSystemName.MatchesSimpleExpression(patron, rutaRelativa!, ignoreCase: true))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
