using SManager.Core.Copia;
using SManager.Core.Modelos;
using SManager.Core.Utilidades;
using SManager.Gui.Shared.Modelos;

namespace SManager.Gui.Shared.Servicios;

/// <summary>
/// Simula una sincronización: compara origen y destino sin encolar ni copiar.
/// Reutiliza la misma lógica que el motor en escaneo/polling.
/// </summary>
public static class ServicioAnalisisCambios
{
    /// <summary>Analiza varios pares y devuelve un resumen agregado.</summary>
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

    /// <summary>Recorre el origen del par y clasifica archivos respecto al destino.</summary>
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

                if (!ServicioFiltros.PasaFiltros(info.Name, par))
                {
                    resultado.OmitidosPorFiltro++;
                    resultado.BytesOmitidosFiltro += info.Length;
                    continue;
                }

                if (ComparadorSincronizacion.ObtenerRutaRelativa(par, rutaAbsoluta) is null)
                {
                    continue;
                }

                if (!ComparadorSincronizacion.NecesitaCopia(info, par, indiceDestino))
                {
                    resultado.YaSincronizados++;
                    continue;
                }

                var relativa = ComparadorSincronizacion.ObtenerRutaRelativa(par, rutaAbsoluta)!;
                if (indiceDestino.TryObtener(relativa, out _))
                {
                    resultado.ArchivosModificados++;
                    resultado.BytesModificados += info.Length;
                }
                else
                {
                    resultado.ArchivosNuevos++;
                    resultado.BytesNuevos += info.Length;
                }

                resultado.BytesPendientes += info.Length;
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

        progreso?.Report((par.Nombre, revisados));
        return resultado;
    }
}
