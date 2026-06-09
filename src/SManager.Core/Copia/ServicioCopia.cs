using SManager.Core.Modelos;
using SManager.Core.Motor;
using SManager.Core.Utilidades;

namespace SManager.Core.Copia;

/// <summary>
/// Único punto de copia física origen → destino con reintentos y backoff.
/// Sincronización unidireccional: copia altas y cambios; nunca borra en destino
/// aunque el archivo desaparezca del origen (no es espejo bidireccional).
/// </summary>
public sealed class ServicioCopia
{
    private const int MaxIntentos = 5;
    private const string SufijoArchivoTemporal = ".smanager.tmp";

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
                var bytesEsperados = infoArchivo.Length;
                CopiarArchivoAtomico(
                    infoArchivo.FullName,
                    rutaCompletaDestino,
                    estado,
                    rutaCompletaOrigen,
                    bytesEsperados);

                var accion = esNuevo ? "Nuevo" : "Actualizado";
                var tipo = esNuevo ? "NUEVO" : "ACTUALIZADO";
                estado.RegistrarActividad(tipo, rutaRelativa, par.IdPar);
                estado.EncolarLog(par.IdPar, "INFO", $"{prefijo}{accion}: {rutaRelativa}");
                estado.ColaEstadisticas.Enqueue(new EstadisticaPar(
                    par.IdPar, 1, 0, DateTime.UtcNow, "OK"));
                estado.Metricas.IncrementarCopiados();
                estado.Metricas.SumarBytesEscritos(bytesEsperados);
                ActualizarContadoresPar(par, copiados: 1, errores: 0);
                return new ResultadoCopia(1, 0);
            }
            catch (OperationCanceledException)
            {
                throw;
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
        if (!estado.ColaHidratacion.IntentarEncolar(new TrabajoHidratacion(idPar, ruta)))
        {
            estado.HidratacionesActivas.TryRemove(ruta, out _);
            estado.EncolarLog(idPar, "WARN", $"Cola de hidratación llena; reintentando: {Path.GetFileName(ruta)}");
        }
    }

    private static void ActualizarContadoresPar(ParSincronizacion par, int copiados, int errores)
    {
        par.TotalCopiados += copiados;
        par.TotalErrores += errores;
    }

    /// <summary>Escribe en .smanager.tmp y renombra; el destino previo no se toca hasta el final.</summary>
    private static void CopiarArchivoAtomico(
        string origen,
        string destinoFinal,
        EstadoMotor estado,
        string claveProgreso,
        long bytesTotales)
    {
        var rutaTemporal = destinoFinal + SufijoArchivoTemporal;
        EliminarTemporalSiExiste(rutaTemporal);

        try
        {
            CopiarArchivoConProgreso(origen, rutaTemporal, estado, claveProgreso, bytesTotales);

            var tamCopiado = new FileInfo(rutaTemporal).Length;
            if (bytesTotales >= 0 && tamCopiado != bytesTotales)
            {
                throw new IOException(
                    $"Verificación de tamaño fallida: origen {bytesTotales} B, temporal {tamCopiado} B");
            }

            File.Move(rutaTemporal, destinoFinal, overwrite: true);
        }
        catch
        {
            EliminarTemporalSiExiste(rutaTemporal);
            throw;
        }
    }

    private static void EliminarTemporalSiExiste(string rutaTemporal)
    {
        if (!File.Exists(rutaTemporal))
        {
            return;
        }

        try
        {
            File.Delete(rutaTemporal);
        }
        catch
        {
            // Limpieza best-effort; el rename fallará si el temporal está corrupto.
        }
    }

    /// <summary>Copia por streaming reportando bytes al monitor (IPC cada ~5% o 512 KiB).</summary>
    private static void CopiarArchivoConProgreso(
        string origen,
        string destino,
        EstadoMotor estado,
        string claveProgreso,
        long bytesTotales)
    {
        const int tamanoBuffer = 1024 * 1024;
        var buffer = new byte[tamanoBuffer];
        long bytesCopiados = 0;
        long ultimoReporte = 0;
        var umbralReporte = bytesTotales > 0
            ? Math.Max(bytesTotales / 20, 512 * 1024)
            : 512 * 1024;

        using var flujoOrigen = new FileStream(origen, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var flujoDestino = new FileStream(destino, FileMode.Create, FileAccess.Write, FileShare.None);

        int leidos;
        while ((leidos = flujoOrigen.Read(buffer, 0, buffer.Length)) > 0)
        {
            // Permite salir pronto cuando el usuario pulsa Detener.
            if (estado.SolicitudParadaCopiadores)
            {
                throw new OperationCanceledException("Copia interrumpida por apagado del demonio.");
            }

            flujoDestino.Write(buffer, 0, leidos);
            bytesCopiados += leidos;

            if (bytesCopiados - ultimoReporte >= umbralReporte)
            {
                ReportarProgresoCopia(estado, claveProgreso, bytesTotales, bytesCopiados);
                ultimoReporte = bytesCopiados;
            }
        }

        ReportarProgresoCopia(estado, claveProgreso, bytesTotales, bytesCopiados);
    }

    private static void ReportarProgresoCopia(
        EstadoMotor estado,
        string claveProgreso,
        long bytesTotales,
        long bytesCopiados)
    {
        if (!estado.CopiasEnCurso.TryGetValue(claveProgreso, out var actual))
        {
            return;
        }

        estado.CopiasEnCurso[claveProgreso] = actual with
        {
            BytesTotales = bytesTotales,
            BytesCopiados = bytesCopiados
        };
    }
}

public readonly record struct ResultadoCopia(int Copiados, int Errores)
{
    public static ResultadoCopia Vacio => new(0, 0);
}
