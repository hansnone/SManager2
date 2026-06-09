using SManager.Ipc;

namespace SManager.Gui.Shared;

/// <summary>Resultado de borrar un perfil y sus datos locales en %LOCALAPPDATA%\SManager2.</summary>
public sealed class ResultadoEliminacionPerfil
{
    public bool Exito { get; init; }

    public IReadOnlyList<string> ElementosEliminados { get; init; } = [];

    public IReadOnlyList<string> Advertencias { get; init; } = [];

    public string? MensajeError { get; init; }
}

/// <summary>
/// Elimina carpetas y archivos del perfil en disco.
/// No borra archivos ya copiados en destino ni el JSON personalizado salvo que se pida explícitamente.
/// </summary>
public static class ServicioEliminacionPerfil
{
    private const int MaxReintentosBorrado = 5;

    /// <summary>
    /// Borra el perfil del sistema local.
    /// Requiere demonio detenido.
    /// </summary>
    public static ResultadoEliminacionPerfil Eliminar(
        string nombrePerfil,
        ServicioIpc ipc,
        bool eliminarJsonPersonalizado = false)
    {
        ServicioConfiguracionGui.ValidarNombrePerfil(nombrePerfil);

        if (ipc.EstaDemonioEnEjecucion(nombrePerfil))
        {
            return new ResultadoEliminacionPerfil
            {
                Exito = false,
                MensajeError = $"El demonio del perfil '{nombrePerfil}' está en ejecución. Deténlo antes de eliminar el perfil."
            };
        }

        // Leer ruta personalizada antes de borrar la carpeta IPC (preferencias.json vive ahí).
        var rutaPersonalizada = ResolvedorRutasConfiguracion.ObtenerRutaPersonalizada(nombrePerfil);
        var eliminados = new List<string>();
        var advertencias = new List<string>();
        var errores = new List<string>();

        ipc.EliminarPid(nombrePerfil);

        // Rutas sin crear carpetas: evita recrear el perfil mientras se borra.
        var rutaLog = RutasDatos.ResolverRutaLog(nombrePerfil);
        var carpetaIpc = RutasDatos.ResolverCarpetaPerfilIpc(nombrePerfil);
        var carpetaConfig = RutasDatos.ResolverCarpetaConfiguracionPerfil(nombrePerfil);

        EliminarArchivoSiExiste(rutaLog, "Log del perfil", eliminados, errores);
        EliminarDirectorioSiExiste(carpetaIpc, "Datos IPC del perfil (estado, PID, preferencias)", eliminados, errores);
        EliminarDirectorioSiExiste(carpetaConfig, "Carpeta de configuración por defecto", eliminados, errores);

        if (!string.IsNullOrWhiteSpace(rutaPersonalizada))
        {
            if (eliminarJsonPersonalizado)
            {
                EliminarArchivoSiExiste(
                    rutaPersonalizada,
                    "JSON de configuración personalizado",
                    eliminados,
                    errores);
            }
            else
            {
                advertencias.Add($"Se conserva el JSON personalizado: {rutaPersonalizada}");
            }
        }

        if (errores.Count > 0)
        {
            return new ResultadoEliminacionPerfil
            {
                Exito = false,
                ElementosEliminados = eliminados,
                Advertencias = advertencias,
                MensajeError = string.Join(Environment.NewLine, errores)
            };
        }

        if (ExistenDatosLocales(nombrePerfil))
        {
            return new ResultadoEliminacionPerfil
            {
                Exito = false,
                ElementosEliminados = eliminados,
                Advertencias = advertencias,
                MensajeError =
                    "Quedaron restos del perfil en disco (¿archivo en uso?). Cierra SManager, detén el demonio e inténtalo de nuevo."
            };
        }

        if (eliminados.Count == 0 && advertencias.Count == 0)
        {
            advertencias.Add("No había datos locales del perfil (ya estaba vacío o no existía).");
        }

        return new ResultadoEliminacionPerfil
        {
            Exito = true,
            ElementosEliminados = eliminados,
            Advertencias = advertencias
        };
    }

    /// <summary>Indica si quedan rastros del perfil en disco.</summary>
    public static bool ExistenDatosLocales(string nombrePerfil)
    {
        ServicioConfiguracionGui.ValidarNombrePerfil(nombrePerfil);

        return Directory.Exists(RutasDatos.ResolverCarpetaConfiguracionPerfil(nombrePerfil))
               || Directory.Exists(RutasDatos.ResolverCarpetaPerfilIpc(nombrePerfil))
               || File.Exists(RutasDatos.ResolverRutaLog(nombrePerfil));
    }

    private static void EliminarArchivoSiExiste(
        string ruta,
        string descripcion,
        ICollection<string> eliminados,
        ICollection<string> errores)
    {
        if (!File.Exists(ruta))
        {
            return;
        }

        try
        {
            QuitarAtributoSoloLectura(ruta);
            for (var intento = 0; intento < MaxReintentosBorrado; intento++)
            {
                try
                {
                    File.Delete(ruta);
                    eliminados.Add(descripcion);
                    return;
                }
                catch (IOException) when (intento < MaxReintentosBorrado - 1)
                {
                    Thread.Sleep(80);
                }
            }
        }
        catch (Exception ex)
        {
            errores.Add($"No se pudo eliminar {descripcion}: {ex.Message}");
        }
    }

    private static void EliminarDirectorioSiExiste(
        string ruta,
        string descripcion,
        ICollection<string> eliminados,
        ICollection<string> errores)
    {
        if (!Directory.Exists(ruta))
        {
            return;
        }

        try
        {
            for (var intento = 0; intento < MaxReintentosBorrado; intento++)
            {
                try
                {
                    Directory.Delete(ruta, recursive: true);
                    if (!Directory.Exists(ruta))
                    {
                        eliminados.Add(descripcion);
                        return;
                    }
                }
                catch (IOException) when (intento < MaxReintentosBorrado - 1)
                {
                    Thread.Sleep(120);
                }
            }

            errores.Add($"No se pudo eliminar {descripcion}: la carpeta sigue existiendo.");
        }
        catch (Exception ex)
        {
            errores.Add($"No se pudo eliminar {descripcion}: {ex.Message}");
        }
    }

    private static void QuitarAtributoSoloLectura(string ruta)
    {
        try
        {
            var atributos = File.GetAttributes(ruta);
            if ((atributos & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(ruta, atributos & ~FileAttributes.ReadOnly);
            }
        }
        catch
        {
            // best-effort
        }
    }
}
