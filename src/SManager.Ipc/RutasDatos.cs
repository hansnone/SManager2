namespace SManager.Ipc;

/// <summary>
/// Rutas de datos por usuario en %LOCALAPPDATA%\SManager2.
/// Cada perfil aísla estado, control, PID y configuración activa.
/// </summary>
public static class RutasDatos
{
    public const string NombreCarpetaRaiz = "SManager2";
    public const string NombreCarpetaPerfilesConfiguracion = "Perfiles configuracion";
    public const string NombreArchivoConfiguracion = "configuracion.json";

    /// <summary>Raíz de datos del usuario actual.</summary>
    public static string ObtenerRaiz()
    {
        var raiz = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            NombreCarpetaRaiz);

        Directory.CreateDirectory(raiz);
        return raiz;
    }

    /// <summary>Raíz donde la GUI guarda JSON editables por el usuario.</summary>
    public static string ObtenerCarpetaPerfilesConfiguracion()
    {
        var ruta = Path.Combine(ObtenerRaiz(), NombreCarpetaPerfilesConfiguracion);
        Directory.CreateDirectory(ruta);
        return ruta;
    }

    /// <summary>Carpeta de configuración de un perfil: Perfiles configuracion\{nombre}\</summary>
    public static string ObtenerCarpetaConfiguracionPerfil(string nombrePerfil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombrePerfil);
        var ruta = Path.Combine(ObtenerCarpetaPerfilesConfiguracion(), nombrePerfil);
        Directory.CreateDirectory(ruta);
        return ruta;
    }

    /// <summary>Ruta JSON editable del perfil en Perfiles configuracion.</summary>
    public static string ObtenerRutaConfiguracionUsuario(string nombrePerfil) =>
        Path.Combine(ObtenerCarpetaConfiguracionPerfil(nombrePerfil), NombreArchivoConfiguracion);

    /// <summary>Carpeta de estado IPC del demonio (estado, control, PID).</summary>
    public static string ObtenerCarpetaPerfil(string nombrePerfil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombrePerfil);
        var ruta = Path.Combine(ObtenerRaiz(), "Perfiles", nombrePerfil);
        Directory.CreateDirectory(ruta);
        return ruta;
    }

    /// <summary>Perfiles con carpeta en Perfiles configuracion y/o estado IPC.</summary>
    public static IReadOnlyList<string> ListarNombresPerfiles()
    {
        var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var dirConfig = ObtenerCarpetaPerfilesConfiguracion();
        if (Directory.Exists(dirConfig))
        {
            foreach (var dir in Directory.GetDirectories(dirConfig))
            {
                nombres.Add(Path.GetFileName(dir));
            }
        }

        var dirIpc = Path.Combine(ObtenerRaiz(), "Perfiles");
        if (Directory.Exists(dirIpc))
        {
            foreach (var dir in Directory.GetDirectories(dirIpc))
            {
                nombres.Add(Path.GetFileName(dir));
            }
        }

        if (nombres.Count == 0)
        {
            nombres.Add("General");
        }

        return nombres.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string ObtenerRutaEstado(string nombrePerfil) =>
        Path.Combine(ObtenerCarpetaPerfil(nombrePerfil), "estado.json");

    public static string ObtenerRutaControl(string nombrePerfil) =>
        Path.Combine(ObtenerCarpetaPerfil(nombrePerfil), "control.json");

    public static string ObtenerRutaPid(string nombrePerfil) =>
        Path.Combine(ObtenerCarpetaPerfil(nombrePerfil), "smanager.pid");

    public static string ObtenerRutaConfiguracionActiva(string nombrePerfil) =>
        Path.Combine(ObtenerCarpetaPerfil(nombrePerfil), "configuracion_activa.json");

    public static string ObtenerRutaLog(string nombrePerfil)
    {
        var dirLogs = Path.Combine(ObtenerRaiz(), "Logs");
        Directory.CreateDirectory(dirLogs);
        return Path.Combine(dirLogs, $"smanager_{nombrePerfil}.log");
    }

    /// <summary>Ruta al ejecutable del host (demonio/servicio).</summary>
    public static string ResolverRutaHost() => ResolvedorEjecutables.ResolverRutaHost();
}
