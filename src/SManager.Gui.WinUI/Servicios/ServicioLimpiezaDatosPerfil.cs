using SManager.Ipc;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Resultado de vaciar logs, telemetría IPC y datos efímeros de un perfil.</summary>
public sealed class ResultadoLimpiezaDatosPerfil
{
    public IReadOnlyList<string> ElementosLimpiados { get; init; } = [];

    public IReadOnlyList<string> Errores { get; init; } = [];

    public bool Exito => Errores.Count == 0;
}

/// <summary>
/// Borra datos derivados del perfil (log, estado IPC, control).
/// No toca configuracion.json ni archivos copiados en destino.
/// </summary>
public static class ServicioLimpiezaDatosPerfil
{
    /// <summary>Limpia telemetría y log del perfil indicado.</summary>
    public static ResultadoLimpiezaDatosPerfil Limpiar(string nombrePerfil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombrePerfil);

        var limpiados = new List<string>();
        var errores = new List<string>();

        VaciarArchivoLog(nombrePerfil, limpiados, errores);
        EliminarSiExiste(
            RutasDatos.ResolverRutaEstado(nombrePerfil),
            "Telemetría IPC (estado.json)",
            limpiados,
            errores);
        EliminarSiExiste(
            RutasDatos.ResolverRutaControl(nombrePerfil),
            "Comandos pendientes (control.json)",
            limpiados,
            errores);

        return new ResultadoLimpiezaDatosPerfil
        {
            ElementosLimpiados = limpiados,
            Errores = errores
        };
    }

    /// <summary>Trunca el log a vacío para que el demonio pueda seguir escribiendo tras reiniciar.</summary>
    private static void VaciarArchivoLog(
        string nombrePerfil,
        ICollection<string> limpiados,
        ICollection<string> errores)
    {
        var rutaLog = RutasDatos.ResolverRutaLog(nombrePerfil);
        try
        {
            if (!File.Exists(rutaLog))
            {
                return;
            }

            File.WriteAllText(rutaLog, string.Empty);
            limpiados.Add("Archivo de log del perfil");
        }
        catch (Exception ex)
        {
            errores.Add($"No se pudo vaciar el log ({ex.Message})");
        }
    }

    private static void EliminarSiExiste(
        string ruta,
        string descripcion,
        ICollection<string> limpiados,
        ICollection<string> errores)
    {
        if (!File.Exists(ruta))
        {
            return;
        }

        try
        {
            File.Delete(ruta);
            limpiados.Add(descripcion);
        }
        catch (Exception ex)
        {
            errores.Add($"No se pudo eliminar {descripcion} ({ex.Message})");
        }
    }
}
