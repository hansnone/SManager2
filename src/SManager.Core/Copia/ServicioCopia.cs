using SManager.Core.Modelos;
using SManager.Core.Motor;
using SManager.Core.Utilidades;

namespace SManager.Core.Copia;

/// <summary>Único punto de copia física origen → destino con reintentos y backoff.</summary>
public sealed class ServicioCopia
{
    private const int MaxIntentos = 5;

    public ResultadoCopia EjecutarCopiaCondicional(
        EstadoMotor estado,
        string rutaCompletaOrigen,
        ParSincronizacion par,
        int idCopiador = 0)
    {
        var prefijo = idCopiador > 0 ? $"Copiador #{idCopiador} -> " : string.Empty;

        FileInfo infoArchivo;
        try
        {
            infoArchivo = new FileInfo(rutaCompletaOrigen);
            if (!infoArchivo.Exists || infoArchivo.Attributes.HasFlag(FileAttributes.Directory))
            {
                return ResultadoCopia.Vacio;
            }
        }
        catch
        {
            return ResultadoCopia.Vacio;
        }

        if (!ServicioFiltros.PasaFiltros(infoArchivo.Name, par))
        {
            return ResultadoCopia.Vacio;
        }

        if (OneDrivePlaceholder.EsPlaceholder(infoArchivo.Attributes))
        {
            EncolarHidratacion(estado, par.IdPar, infoArchivo.FullName);
            return ResultadoCopia.Vacio;
        }

        var rutaRaizOrigen = Path.GetFullPath(par.RutaOrigen.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var rutaRaizDestino = Path.GetFullPath(par.RutaDestino.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var rutaRelativa = infoArchivo.FullName[rutaRaizOrigen.Length..].TrimStart('\\', '/');
        var rutaCompletaDestino = Path.Combine(rutaRaizDestino, rutaRelativa);
        var directorioDestino = Path.GetDirectoryName(rutaCompletaDestino);

        if (!string.IsNullOrEmpty(directorioDestino) && !Directory.Exists(directorioDestino))
        {
            try
            {
                Directory.CreateDirectory(directorioDestino);
            }
            catch (Exception ex)
            {
                estado.EncolarLog(par.IdPar, "ERROR", $"Fallo creación directorio: {directorioDestino} ({ex.Message})");
                return new ResultadoCopia(0, 1);
            }
        }

        var necesitaCopia = false;
        var esNuevo = false;

        if (!File.Exists(rutaCompletaDestino))
        {
            necesitaCopia = true;
            esNuevo = true;
        }
        else
        {
            try
            {
                var infoDestino = new FileInfo(rutaCompletaDestino);
                var diff = (infoArchivo.LastWriteTimeUtc - infoDestino.LastWriteTimeUtc).TotalSeconds;
                if (diff > 2 || (infoArchivo.Length != infoDestino.Length && Math.Abs(diff) <= 2))
                {
                    necesitaCopia = true;
                }
            }
            catch
            {
                necesitaCopia = true;
            }
        }

        if (!necesitaCopia)
        {
            return ResultadoCopia.Vacio;
        }

        for (var intento = 1; intento <= MaxIntentos; intento++)
        {
            try
            {
                File.Copy(infoArchivo.FullName, rutaCompletaDestino, overwrite: true);
                var accion = esNuevo ? "Nuevo" : "Actualizado";
                var tipo = esNuevo ? "NUEVO" : "ACTUALIZADO";
                estado.RegistrarActividad(tipo, rutaRelativa, par.IdPar);
                estado.EncolarLog(par.IdPar, "INFO", $"{prefijo}{accion}: {rutaRelativa}");
                estado.ColaEstadisticas.Enqueue(new EstadisticaPar(
                    par.IdPar, 1, 0, DateTime.UtcNow, "OK"));
                estado.Metricas.IncrementarCopiados();
                ActualizarContadoresPar(par, copiados: 1, errores: 0);
                return new ResultadoCopia(1, 0);
            }
            catch (Exception ex) when (intento < MaxIntentos)
            {
                var esperaMs = Math.Min(4000, 500 * (int)Math.Pow(2, intento - 1));
                Thread.Sleep(esperaMs);
                _ = ex;
            }
            catch (Exception ex)
            {
                estado.RegistrarActividad("ERROR", rutaRelativa, par.IdPar, ex.Message);
                estado.EncolarLog(par.IdPar, "ERROR", $"{prefijo}{rutaRelativa}: {ex.Message}");
                estado.ColaEstadisticas.Enqueue(new EstadisticaPar(
                    par.IdPar, 0, 1, DateTime.UtcNow, "ERROR"));
                estado.Metricas.IncrementarErrores();
                ActualizarContadoresPar(par, copiados: 0, errores: 1);
                return new ResultadoCopia(0, 1);
            }
        }

        return ResultadoCopia.Vacio;
    }

    private static void EncolarHidratacion(EstadoMotor estado, string idPar, string ruta)
    {
        if (!estado.HidratacionesActivas.TryAdd(ruta, 0))
        {
            return;
        }

        estado.EncolarLog(idPar, "PENDIENTE", $"Hidratación OneDrive: {Path.GetFileName(ruta)}");
        estado.ColaHidratacion.IntentarEncolar(new TrabajoHidratacion(idPar, ruta));
    }

    private static void ActualizarContadoresPar(ParSincronizacion par, int copiados, int errores)
    {
        par.TotalCopiados += copiados;
        par.TotalErrores += errores;
    }
}

public readonly record struct ResultadoCopia(int Copiados, int Errores)
{
    public static ResultadoCopia Vacio => new(0, 0);
}
