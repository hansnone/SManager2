using System.Diagnostics;
using System.Text;
using SManager.Ipc;

// Consola en UTF-8 para que la GUI muestre bien tildes y eñes en stderr.
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var ipc = new ServicioIpc();

if (args.Length == 0)
{
    MostrarAyuda();
    return 1;
}

var comando = args[0].ToLowerInvariant();
var perfil = ObtenerArgumento(args, "-perfil") ?? "General";
var configPath = ObtenerArgumento(args, "-configpath") ?? ObtenerArgumento(args, "-config");

try
{
    return comando switch
    {
        "help" or "-h" or "--help" => MostrarAyuda(),
        "start" => await EjecutarInicioAsync(ipc, perfil, configPath).ConfigureAwait(false),
        "stop" => await EjecutarParadaAsync(ipc, perfil).ConfigureAwait(false),
        "status" => await EjecutarEstadoAsync(ipc, perfil).ConfigureAwait(false),
        "reload" => await EjecutarRecargaAsync(ipc, perfil).ConfigureAwait(false),
        "perfiles" => EjecutarListarPerfiles(ipc),
        _ => Error($"Comando desconocido: '{comando}'. Usa: smanager help")
    };
}
catch (Exception ex)
{
    return Error(ex.Message);
}

static int MostrarAyuda()
{
    Console.WriteLine("""
        SManager 2.0 — Gestor de sincronización unidireccional

        Uso: smanager <comando> [-Perfil <nombre>] [-ConfigPath <ruta.json>]

        Comandos:
          start     Inicia el demonio para un perfil
          stop      Apagado ordenado del demonio
          status    Una línea de estado (exit 0=RUN, 1=STOP)
          reload    Fuerza recarga del JSON de configuración
          perfiles  Lista perfiles conocidos en %LOCALAPPDATA%\SManager2
          help      Muestra esta ayuda

        La GUI (SManager.Gui) y la CLI comparten el mismo demonio vía IPC en disco.
        """);
    return 0;
}

static async Task<int> EjecutarInicioAsync(ServicioIpc ipc, string perfil, string? configPath)
{
    if (string.IsNullOrWhiteSpace(configPath))
    {
        return Error("start requiere -ConfigPath con la ruta absoluta al JSON.");
    }

    configPath = Path.GetFullPath(configPath);
    if (!File.Exists(configPath))
    {
        return Error($"No existe el archivo de configuración: {configPath}");
    }

    if (ipc.EstaDemonioEnEjecucion(perfil))
    {
        return Error($"Ya hay un demonio en ejecución para el perfil '{perfil}'.");
    }

    ipc.EliminarPid(perfil);

    var rutaHost = RutasDatos.ResolverRutaHost();
    if (!File.Exists(rutaHost))
    {
        return Error($"No se encuentra SManager.Host.exe en: {rutaHost}");
    }

    var directorioHost = Path.GetDirectoryName(rutaHost) ?? AppContext.BaseDirectory;
    var psi = new ProcessStartInfo
    {
        FileName = rutaHost,
        Arguments = $"--demonio --perfil \"{perfil}\" --config \"{configPath}\"",
        UseShellExecute = false,
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden,
        WorkingDirectory = directorioHost,
        RedirectStandardError = true
    };

    var proceso = Process.Start(psi);
    if (proceso is null)
    {
        return Error("No se pudo iniciar SManager.Host.exe");
    }

    for (var i = 0; i < 40; i++)
    {
        await Task.Delay(250).ConfigureAwait(false);
        if (ipc.EstaDemonioEnEjecucion(perfil))
        {
            Console.WriteLine($"Demonio iniciado (perfil: {perfil}, PID: {File.ReadAllText(RutasDatos.ObtenerRutaPid(perfil)).Trim()}).");
            return 0;
        }
    }

    var errorHost = await proceso.StandardError.ReadToEndAsync().ConfigureAwait(false);
    if (!proceso.HasExited)
    {
        try { proceso.Kill(entireProcessTree: true); } catch { /* best-effort */ }
    }

    var detalle = string.IsNullOrWhiteSpace(errorHost)
        ? "Revisa el log en %LOCALAPPDATA%\\SManager2\\Logs\\"
        : errorHost.Trim();

    return Error($"El demonio se lanzó pero no registró PID. {detalle}");
}

static async Task<int> EjecutarParadaAsync(ServicioIpc ipc, string perfil)
{
    if (!ipc.EstaDemonioEnEjecucion(perfil))
    {
        Console.WriteLine($"No hay demonio activo para el perfil '{perfil}'.");
        await ipc.LimpiarComandoAsync(perfil).ConfigureAwait(false);
        ipc.EliminarPid(perfil);
        return 0;
    }

    await ipc.EnviarComandoAsync(perfil, ComandoControl.Apagar).ConfigureAwait(false);
    Console.WriteLine($"Señal APAGAR enviada al perfil '{perfil}'. Esperando cierre ordenado...");

    var limite = DateTime.UtcNow.AddMinutes(12);
    while (DateTime.UtcNow < limite)
    {
        await Task.Delay(500).ConfigureAwait(false);
        if (!ipc.EstaDemonioEnEjecucion(perfil))
        {
            Console.WriteLine("Demonio detenido correctamente.");
            return 0;
        }
    }

    return Error("Timeout esperando al demonio. Revisa el log o termina el proceso manualmente.");
}

static async Task<int> EjecutarEstadoAsync(ServicioIpc ipc, string perfil)
{
    if (!ipc.EstaDemonioEnEjecucion(perfil))
    {
        Console.WriteLine($"STOP  perfil={perfil}  (sin demonio)");
        return 1;
    }

    var estado = await ipc.LeerEstadoAsync(perfil).ConfigureAwait(false);
    if (estado is null)
    {
        Console.WriteLine($"RUN   perfil={perfil}  (sin telemetría aún)");
        return 0;
    }

    Console.WriteLine(
        $"RUN   perfil={estado.Perfil}  pid={estado.Pid}  cola={estado.ColaCopiaPendiente}  copiados={estado.Totales.Copiados}  errores={estado.Totales.Errores}");
    return 0;
}

static async Task<int> EjecutarRecargaAsync(ServicioIpc ipc, string perfil)
{
    if (!ipc.EstaDemonioEnEjecucion(perfil))
    {
        return Error($"No hay demonio activo para '{perfil}'.");
    }

    await ipc.EnviarComandoAsync(perfil, ComandoControl.Recargar).ConfigureAwait(false);
    Console.WriteLine($"Señal RECARGAR enviada al perfil '{perfil}'.");
    return 0;
}

static int EjecutarListarPerfiles(ServicioIpc ipc)
{
    var perfiles = ipc.ListarPerfiles();
    if (perfiles.Count == 0)
    {
        Console.WriteLine("(sin perfiles registrados)");
        return 0;
    }

    foreach (var p in perfiles)
    {
        var estado = ipc.EstaDemonioEnEjecucion(p) ? "RUN" : "STOP";
        Console.WriteLine($"{estado,-5} {p}");
    }

    return 0;
}

static string? ObtenerArgumento(string[] args, string nombre)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(nombre, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

static int Error(string mensaje)
{
    Console.Error.WriteLine($"ERROR: {mensaje}");
    return 1;
}
