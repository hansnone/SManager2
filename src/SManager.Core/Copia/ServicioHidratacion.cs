using SManager.Core.Modelos;
using SManager.Core.Motor;
using SManager.Core.Utilidades;

namespace SManager.Core.Copia;

/// <summary>Fuerza la descarga de placeholders OneDrive leyendo el primer byte.</summary>
public sealed class ServicioHidratacion
{
    public async Task<bool> ForzarDescargaAsync(string ruta, int timeoutMs, CancellationToken cancelacion)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
        cts.CancelAfter(timeoutMs);

        try
        {
            return await Task.Run(() =>
            {
                using var flujo = new FileStream(
                    ruta,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);
                return flujo.ReadByte() >= 0;
            }, cts.Token).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    public async Task ProcesarTrabajoAsync(
        EstadoMotor estado,
        TrabajoHidratacion trabajo,
        int idHidratador,
        CancellationToken cancelacion)
    {
        var idPar = trabajo.IdPar;
        var rutaOrigen = trabajo.RutaCompleta;

        try
        {
            ParSincronizacion? par;
            lock (estado.CandadoPares)
            {
                par = estado.Pares.FirstOrDefault(p => p.IdPar == idPar);
            }

            if (par is null || !File.Exists(rutaOrigen))
            {
                return;
            }

            var nombre = Path.GetFileName(rutaOrigen);
            estado.EncolarLog(idPar, "INFO", $"Hidratador #{idHidratador} descargando: {nombre}");

            var timeoutMs = Math.Clamp(estado.Config.TimeoutHidratacionSegundos, 1, 3600) * 1000;
            var inicio = DateTime.UtcNow;
            var ok = await ForzarDescargaAsync(rutaOrigen, timeoutMs, cancelacion).ConfigureAwait(false);
            var segundos = (int)(DateTime.UtcNow - inicio).TotalSeconds;

            if (!ok)
            {
                estado.EncolarLog(idPar, "ERROR", $"Timeout ({segundos}s) descargando: {nombre}");
                estado.ColaEstadisticas.Enqueue(new EstadisticaPar(idPar, 0, 1, DateTime.UtcNow, "TIMEOUT"));
                estado.Metricas.IncrementarErrores();
                par.TotalErrores++;
                return;
            }

            var info = new FileInfo(rutaOrigen);
            if (OneDrivePlaceholder.EsPlaceholder(info.Attributes))
            {
                estado.EncolarLog(idPar, "ERROR", $"Sigue como placeholder: {nombre}");
                estado.Metricas.IncrementarErrores();
                par.TotalErrores++;
                return;
            }

            estado.EncolarLog(idPar, "INFO", $"Descargado en {segundos}s, encolando copia: {nombre}");
            if (estado.AceptarNuevosTrabajos)
            {
                estado.ColaCopia.IntentarEncolar(
                    new TrabajoCopia(idPar, rutaOrigen),
                    estado.Metricas);
            }
        }
        finally
        {
            estado.HidratacionesActivas.TryRemove(rutaOrigen, out _);
        }
    }
}
