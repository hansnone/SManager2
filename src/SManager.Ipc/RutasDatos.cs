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

    // --- Resolución SIN crear carpetas (lecturas, listados, borrado) ---

    /// <summary>Raíz de datos sin crear carpetas.</summary>
    public static string ResolverRaiz() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            NombreCarpetaRaiz);

    public static string ResolverCarpetaPerfilesConfiguracion() =>
        Path.Combine(ResolverRaiz(), NombreCarpetaPerfilesConfiguracion);

    public static string ResolverCarpetaConfiguracionPerfil(string nombrePerfil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombrePerfil);
        return Path.Combine(ResolverCarpetaPerfilesConfiguracion(), nombrePerfil);
    }

    public static string ResolverCarpetaPerfilIpc(string nombrePerfil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombrePerfil);
        return Path.Combine(ResolverRaiz(), "Perfiles", nombrePerfil);
    }

    public static string ResolverRutaConfiguracionUsuario(string nombrePerfil) =>
        Path.Combine(ResolverCarpetaConfiguracionPerfil(nombrePerfil), NombreArchivoConfiguracion);

    public static string ResolverRutaEstado(string nombrePerfil) =>
        Path.Combine(ResolverCarpetaPerfilIpc(nombrePerfil), "estado.json");

    public static string ResolverRutaControl(string nombrePerfil) =>
        Path.Combine(ResolverCarpetaPerfilIpc(nombrePerfil), "control.json");

    public static string ResolverRutaPid(string nombrePerfil) =>
        Path.Combine(ResolverCarpetaPerfilIpc(nombrePerfil), "smanager.pid");

    public static string ResolverRutaConfiguracionActiva(string nombrePerfil) =>
        Path.Combine(ResolverCarpetaPerfilIpc(nombrePerfil), "configuracion_activa.json");

    public static string ResolverRutaLog(string nombrePerfil) =>
        Path.Combine(ResolverRaiz(), "Logs", $"smanager_{nombrePerfil}.log");

    // --- Obtención CON creación de carpetas (escrituras, arranque del demonio) ---

    /// <summary>Raíz de datos del usuario actual.</summary>
    public static string ObtenerRaiz()
    {
        var raiz = ResolverRaiz();
        Directory.CreateDirectory(raiz);
        return raiz;
    }

    /// <summary>Raíz donde la GUI guarda JSON editables por el usuario.</summary>
    public static string ObtenerCarpetaPerfilesConfiguracion()
    {
        var ruta = ResolverCarpetaPerfilesConfiguracion();
        Directory.CreateDirectory(ruta);
        return ruta;
    }

    /// <summary>Carpeta de configuración de un perfil: Perfiles configuracion\{nombre}\</summary>
    public static string ObtenerCarpetaConfiguracionPerfil(string nombrePerfil)
    {
        var ruta = ResolverCarpetaConfiguracionPerfil(nombrePerfil);
        Directory.CreateDirectory(ruta);
        return ruta;
    }

    /// <summary>Ruta JSON editable del perfil en Perfiles configuracion.</summary>
    public static string ObtenerRutaConfiguracionUsuario(string nombrePerfil) =>
        Path.Combine(ObtenerCarpetaConfiguracionPerfil(nombrePerfil), NombreArchivoConfiguracion);

    /// <summary>Carpeta de estado IPC del demonio (estado, control, PID).</summary>
    public static string ObtenerCarpetaPerfil(string nombrePerfil)
    {
        var ruta = ResolverCarpetaPerfilIpc(nombrePerfil);
        Directory.CreateDirectory(ruta);
        return ruta;
    }

    /// <summary>Perfiles con carpeta en Perfiles configuracion y/o estado IPC.</summary>
    public static IReadOnlyList<string> ListarNombresPerfiles()
    {
        var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var dirConfig = ResolverCarpetaPerfilesConfiguracion();
        if (Directory.Exists(dirConfig))
        {
            foreach (var dir in Directory.GetDirectories(dirConfig))
            {
                nombres.Add(Path.GetFileName(dir));
            }
        }

        var dirIpc = Path.Combine(ResolverRaiz(), "Perfiles");
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
