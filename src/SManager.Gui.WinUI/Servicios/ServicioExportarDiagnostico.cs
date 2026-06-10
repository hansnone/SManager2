using System.Text;
using SManager.Ipc;
using SManager.Ipc.Modelos;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Genera ficheros de registro o diagnóstico para soporte y depuración.</summary>
public static class ServicioExportarDiagnostico
{
    /// <summary>Exporta las líneas visibles del registro a un archivo de texto.</summary>
    public static async Task<string> ExportarRegistroAsync(
        IEnumerable<string> lineas,
        string perfil,
        string? rutaDestino = null)
    {
        var ruta = rutaDestino ?? GenerarRutaDescargas($"SManager_registro_{SanearNombre(perfil)}_{MarcaTiempo()}.txt");
        var contenido = string.Join(Environment.NewLine, lineas);
        await File.WriteAllTextAsync(ruta, contenido, Encoding.UTF8).ConfigureAwait(false);
        return ruta;
    }

    /// <summary>Paquete de diagnóstico: estado, rutas, resumen del log y configuración básica.</summary>
    public static async Task<string> ExportarPaqueteDiagnosticoAsync(
        string perfil,
        string rutaConfiguracion,
        EstadoPerfil? estado,
        string textoRegistroCrudo,
        int lineasVisibles,
        string? rutaDestino = null)
    {
        var ruta = rutaDestino ?? GenerarRutaDescargas($"SManager_diagnostico_{SanearNombre(perfil)}_{MarcaTiempo()}.txt");
        var sb = new StringBuilder();

        sb.AppendLine("=== SManager 2.0 — Diagnóstico ===");
        sb.AppendLine($"Generado: {DateTimeOffset.Now:O}");
        sb.AppendLine($"Perfil: {perfil}");
        sb.AppendLine($"Configuración: {rutaConfiguracion}");
        sb.AppendLine($"Log: {RutasDatos.ResolverRutaLog(perfil)}");
        sb.AppendLine($"Estado IPC: {RutasDatos.ResolverRutaEstado(perfil)}");
        sb.AppendLine();

        if (estado is not null)
        {
            sb.AppendLine("--- Telemetría ---");
            sb.AppendLine($"En ejecución: {estado.EnEjecucion}");
            sb.AppendLine($"Cola pendiente: {estado.ColaCopiaPendiente}");
            sb.AppendLine($"Copiados sesión: {estado.Totales.Copiados}");
            sb.AppendLine($"Errores sesión: {estado.Totales.Errores}");
            sb.AppendLine($"Bytes escritos: {estado.Totales.BytesEscritos}");
            sb.AppendLine($"Actualizado UTC: {estado.ActualizadoUtc}");
            sb.AppendLine();

            if (estado.Pares.Count > 0)
            {
                sb.AppendLine("--- Pares ---");
                foreach (var par in estado.Pares)
                {
                    sb.AppendLine(
                        $"- {par.Nombre}: estado={par.Estado}, copiados={par.Copiados}, errores={par.Errores}");
                }

                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("--- Telemetría ---");
            sb.AppendLine("Demonio detenido o sin estado.json disponible.");
            sb.AppendLine();
        }

        sb.AppendLine($"--- Registro (últimas {Math.Min(200, ContarLineas(textoRegistroCrudo))} líneas) ---");
        foreach (var linea in ObtenerUltimasLineas(textoRegistroCrudo, 200))
        {
            sb.AppendLine(linea);
        }

        sb.AppendLine();
        sb.AppendLine($"--- Fin (líneas visibles en GUI con filtros actuales: {lineasVisibles}) ---");

        await File.WriteAllTextAsync(ruta, sb.ToString(), Encoding.UTF8).ConfigureAwait(false);
        return ruta;
    }

    private static IEnumerable<string> ObtenerUltimasLineas(string texto, int maximo)
    {
        if (string.IsNullOrEmpty(texto))
        {
            yield break;
        }

        var lineas = texto.Split('\n');
        var inicio = Math.Max(0, lineas.Length - maximo);
        for (var i = inicio; i < lineas.Length; i++)
        {
            var limpia = lineas[i].TrimEnd('\r');
            if (!string.IsNullOrWhiteSpace(limpia))
            {
                yield return limpia;
            }
        }
    }

    private static int ContarLineas(string texto) =>
        string.IsNullOrEmpty(texto) ? 0 : texto.Split('\n').Length;

    private static string GenerarRutaDescargas(string nombreArchivo)
    {
        var carpeta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        Directory.CreateDirectory(carpeta);
        return Path.Combine(carpeta, nombreArchivo);
    }

    private static string SanearNombre(string nombre)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            nombre = nombre.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(nombre) ? "perfil" : nombre.Trim();
    }

    private static string MarcaTiempo() => DateTime.Now.ToString("yyyyMMdd_HHmmss");
}
