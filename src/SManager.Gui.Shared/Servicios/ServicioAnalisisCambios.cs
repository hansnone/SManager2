using SManager.Core.Copia;
using SManager.Core.Modelos;
using SManager.Core.Utilidades;
using SManager.Gui.Shared.Modelos;

namespace SManager.Gui.Shared.Servicios;

/// <summary>
/// Simula una sincronización: compara origen y destino sin encolar, copiar ni eliminar nada.
/// Reutiliza la misma lógica del motor y genera además la vista previa detallada de archivos.
/// </summary>
public static class ServicioAnalisisCambios
{
    // Límite máximo de archivos detallados por par para mantener la ventana ágil incluso con decenas de miles de archivos.
    public const int MaximoArchivosDetalladosPorPar = 500;

    /// <summary>Analiza varios pares y devuelve un resumen agregado junto con el listado de archivos previstos.</summary>
    public static ResultadoAnalisisGlobal AnalizarPares(
        IEnumerable<ParSincronizacion> pares,
        CancellationToken cancelacion = default,
        IProgress<(string nombrePar, int revisados)>? progreso = null)
    {
        var resultados = new List<ResultadoAnalisisPar>();

        foreach (var par in pares)
        {
            cancelacion.ThrowIfCancellationRequested();
            resultados.Add(AnalizarPar(par, cancelacion, progreso));
        }

        return new ResultadoAnalisisGlobal { PorPar = resultados };
    }

    /// <summary>Recorre el origen (y el destino si está en Modo Espejo) y clasifica cada archivo respecto al destino.</summary>
    public static ResultadoAnalisisPar AnalizarPar(
        ParSincronizacion par,
        CancellationToken cancelacion = default,
        IProgress<(string nombrePar, int revisados)>? progreso = null)
    {
        var resultado = new ResultadoAnalisisPar
        {
            IdPar = par.IdPar,
            NombrePar = par.Nombre,
            AvisosRiesgo = ServicioValidacionRiesgoPar.DetectarAvisos(par)
        };

        if (string.IsNullOrWhiteSpace(par.RutaOrigen) || !Directory.Exists(par.RutaOrigen))
        {
            resultado.ErroresAcceso++;
            return resultado;
        }

        var indiceDestino = IndiceMetadatosDestino.Construir(par, cancelacion);
        var revisados = 0;

        foreach (var rutaAbsoluta in Directory.EnumerateFiles(
                     par.RutaOrigen,
                     "*",
                     SearchOption.AllDirectories))
        {
            cancelacion.ThrowIfCancellationRequested();

            try
            {
                var info = new FileInfo(rutaAbsoluta);
                if (!info.Exists)
                {
                    continue;
                }

                revisados++;
                if (revisados % 500 == 0)
                {
                    progreso?.Report((par.Nombre, revisados));
                }

                var relativa = ComparadorSincronizacion.ObtenerRutaRelativa(par, rutaAbsoluta);
                if (relativa is null)
                {
                    continue;
                }

                if (!ServicioFiltros.PasaFiltros(info.Name, par, relativa))
                {
                    resultado.OmitidosPorFiltro++;
                    resultado.BytesOmitidosFiltro += info.Length;
                    continue;
                }

                if (!ComparadorSincronizacion.NecesitaCopia(info, par, indiceDestino))
                {
                    resultado.YaSincronizados++;
                    continue;
                }

                string tipoAccion;
                if (indiceDestino.TryObtener(relativa, out _))
                {
                    resultado.ArchivosModificados++;
                    resultado.BytesModificados += info.Length;
                    tipoAccion = "Modificado";
                }
                else
                {
                    resultado.ArchivosNuevos++;
                    resultado.BytesNuevos += info.Length;
                    tipoAccion = "Nuevo";
                }

                resultado.BytesPendientes += info.Length;
                RegistrarArchivoPrevisto(resultado, par.Nombre, relativa, tipoAccion, info.Length);
            }
            catch (UnauthorizedAccessException)
            {
                resultado.ErroresAcceso++;
            }
            catch (IOException)
            {
                resultado.ErroresAcceso++;
            }
            catch
            {
                resultado.ErroresAcceso++;
            }
        }

        // Si el par está en Modo Espejo, identificamos también los archivos huérfanos en destino que serían eliminados.
        if (par.Modo == ModoSincronizacion.Espejo && Directory.Exists(par.RutaDestino))
        {
            AnalizarHuerfanosEspejo(par, resultado, cancelacion);
        }

        progreso?.Report((par.Nombre, revisados));
        return resultado;
    }

    private static void AnalizarHuerfanosEspejo(
        ParSincronizacion par,
        ResultadoAnalisisPar resultado,
        CancellationToken cancelacion)
    {
        try
        {
            var raizDestino = Path.GetFullPath(
                par.RutaDestino.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var raizOrigen = Path.GetFullPath(
                par.RutaOrigen.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            foreach (var archivoDestino in Directory.EnumerateFiles(
                         par.RutaDestino,
                         "*",
                         SearchOption.AllDirectories))
            {
                cancelacion.ThrowIfCancellationRequested();
                if (archivoDestino.EndsWith(".smanager.tmp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var rel = archivoDestino[raizDestino.Length..].TrimStart('\\', '/');
                var equivOrigen = Path.Combine(raizOrigen, rel);

                if (!File.Exists(equivOrigen) && ServicioFiltros.PasaFiltros(Path.GetFileName(archivoDestino), par, rel))
                {
                    long tamano = 0;
                    try
                    {
                        tamano = new FileInfo(archivoDestino).Length;
                    }
                    catch
                    {
                        // Si no se puede leer el tamaño, sumamos 0.
                    }

                    resultado.ArchivosHuerfanosEspejo++;
                    resultado.BytesHuerfanosEspejo += tamano;
                    RegistrarArchivoPrevisto(resultado, par.Nombre, rel, "Eliminar (espejo)", tamano);
                }
            }
        }
        catch
        {
            resultado.ErroresAcceso++;
        }
    }

    private static void RegistrarArchivoPrevisto(
        ResultadoAnalisisPar resultado,
        string nombrePar,
        string rutaRelativa,
        string tipoAccion,
        long tamanoBytes)
    {
        if (resultado.ArchivosPrevistos.Count < MaximoArchivosDetalladosPorPar)
        {
            resultado.ArchivosPrevistos.Add(
                new ArchivoCambioPrevisto(nombrePar, rutaRelativa, tipoAccion, tamanoBytes));
        }
        else
        {
            resultado.DetalleTruncado = true;
        }
    }
}
