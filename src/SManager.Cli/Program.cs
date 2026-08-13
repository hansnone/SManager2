using System.Diagnostics;
using System.Text;
using SManager.Core.Utilidades;
using SManager.Gui.Shared;
using SManager.Ipc;

// Consola en UTF-8 para que la GUI muestre bien tildes y eñes en stderr.
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var ipc = new ServicioIpc();
var servicioConfig = new ServicioConfiguracionGui();

if (args.Length == 0)
{
    MostrarAyuda();
    return 1;
}

var comando = args[0].ToLowerInvariant();
var perfil = ObtenerArgumento(args, "-perfil") ?? "General";
var configPath = ObtenerArgumento(args, "-configpath") ?? ObtenerArgumento(args, "-config");
var rutaArgumento = ObtenerArgumento(args, "-ruta");

try
{
    return comando switch
    {
        "help" or "-h" or "--help" => MostrarAyuda(),
        "start" => await EjecutarInicioAsync(ipc, servicioConfig, perfil, configPath).ConfigureAwait(false),
        "stop" => await EjecutarParadaAsync(ipc, perfil).ConfigureAwait(false),
        "status" => await EjecutarEstadoAsync(ipc, perfil).ConfigureAwait(false),
        "list" or "ls" or "pares" => EjecutarListarPares(servicioConfig, perfil),
        "set-mode" or "modo" => await EjecutarCambiarModoAsync(ipc, servicioConfig, perfil, args).ConfigureAwait(false),
        "authorize-purge" or "autorizar-purga" => await EjecutarAutorizarPurgaAsync(ipc, servicioConfig, perfil, args).ConfigureAwait(false),
        "reload" => await EjecutarRecargaAsync(ipc, perfil).ConfigureAwait(false),
        "unlock-delete" or "desbloquear-borrado" => await EjecutarDesbloquearBorradoAsync(ipc, perfil).ConfigureAwait(false),
        "perfiles" => EjecutarListarPerfiles(ipc),
        "perfil" => EjecutarGestionPerfil(ipc, perfil, args),
        "config" => EjecutarConfiguracionRuta(servicioConfig, perfil, args),
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
          start            Inicia el demonio para un perfil
          stop             Apagado ordenado del demonio
          status           Una línea de estado (exit 0=RUN, 1=STOP)
          list             Lista los pares del perfil indicando su estado y modo
          set-mode         Cambia el modo de un par (set-mode <idPar|nombre> <0|1|2 | acumulativo|borrado-origen|espejo>)
          autorizar-purga  Autoriza una purga masiva en Modo Espejo (autorizar-purga <idPar|nombre>)
          reload           Fuerza recarga del JSON de configuración
          perfiles         Lista perfiles conocidos en %LOCALAPPDATA%\SManager2
          perfil           Elimina un perfil y sus datos locales (perfil eliminar)
          config           Muestra o cambia la ruta del JSON de configuración
          help             Muestra esta ayuda

        Configuración (ruta del JSON):
          Por defecto (sin -ConfigPath):
            %LOCALAPPDATA%\SManager2\Perfiles configuracion\<perfil>\configuracion.json
          smanager config [-Perfil <nombre>]
            Muestra la ruta efectiva y si es personalizada
          smanager config set [-Perfil <nombre>] -Ruta <ruta.json>
            Usa un JSON en una ubicación personalizada (persistente)
          smanager config reset [-Perfil <nombre>]
            Vuelve a la ubicación por defecto del perfil

        start sin -ConfigPath usa la ruta resuelta (personalizada o por defecto).
        Si el archivo no existe, se crea con valores iniciales.

        Eliminar perfil:
          smanager perfil eliminar [-Perfil <nombre>] [-EliminarJsonPersonalizado]
            Borra carpetas del perfil, log y telemetría IPC.
            El JSON personalizado se conserva salvo -EliminarJsonPersonalizado.

        La GUI y la CLI comparten el mismo demonio vía IPC en disco.
        """);
    return 0;
}

static async Task<int> EjecutarInicioAsync(
    ServicioIpc ipc,
    ServicioConfiguracionGui servicioConfig,
    string perfil,
    string? configPathExplicito)
{
    string rutaConfig;
    if (!string.IsNullOrWhiteSpace(configPathExplicito))
    {
        rutaConfig = Path.GetFullPath(configPathExplicito);
        if (!File.Exists(rutaConfig))
        {
            return Error($"No existe el archivo de configuración: {rutaConfig}");
        }
    }
    else
    {
        rutaConfig = servicioConfig.AsegurarConfiguracionPerfil(perfil);
        Console.WriteLine($"Configuración: {rutaConfig}");
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
        Arguments = $"--demonio --perfil \"{perfil}\" --config \"{rutaConfig}\"",
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
            Console.WriteLine($"Demonio iniciado (perfil: {perfil}, PID: {File.ReadAllText(RutasDatos.ResolverRutaPid(perfil)).Trim()}).");
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

static int EjecutarConfiguracionRuta(ServicioConfiguracionGui servicioConfig, string perfil, string[] args)
{
    var subcomando = args.Length > 1 ? args[1].ToLowerInvariant() : "show";

    return subcomando switch
    {
        "show" or "path" or "ruta" => MostrarRutaConfiguracion(servicioConfig, perfil),
        "set" or "establecer" => EstablecerRutaConfiguracion(servicioConfig, perfil, args),
        "reset" or "restablecer" => RestablecerRutaConfiguracion(servicioConfig, perfil),
        _ => Error($"Subcomando config desconocido: '{subcomando}'. Usa: config, config set, config reset")
    };
}

static int MostrarRutaConfiguracion(ServicioConfiguracionGui servicioConfig, string perfil)
{
    var porDefecto = servicioConfig.ObtenerRutaPorDefecto(perfil);
    var resuelta = servicioConfig.ResolverRutaConfiguracion(perfil);
    var personalizada = servicioConfig.UsaRutaPersonalizada(perfil);

    Console.WriteLine($"Perfil: {perfil}");
    Console.WriteLine($"Ruta por defecto: {porDefecto}");
    Console.WriteLine($"Ruta efectiva:    {resuelta}");
    Console.WriteLine($"Modo:             {(personalizada ? "personalizada" : "por defecto")}");
    Console.WriteLine($"Existe:           {(File.Exists(resuelta) ? "sí" : "no")}");
    return 0;
}

static int EstablecerRutaConfiguracion(ServicioConfiguracionGui servicioConfig, string perfil, string[] args)
{
    var ruta = ObtenerArgumento(args, "-ruta");
    if (string.IsNullOrWhiteSpace(ruta))
    {
        return Error("config set requiere -Ruta con la ruta al archivo .json");
    }

    ruta = Path.GetFullPath(ruta);
    servicioConfig.EstablecerRutaPersonalizada(perfil, ruta);

    if (!File.Exists(ruta))
    {
        servicioConfig.Guardar(ruta, ServicioConfiguracionGui.CrearPorDefecto());
        Console.WriteLine($"Archivo creado: {ruta}");
    }

    Console.WriteLine($"Perfil '{perfil}' usará la configuración personalizada:");
    Console.WriteLine(ruta);
    return 0;
}

static int RestablecerRutaConfiguracion(ServicioConfiguracionGui servicioConfig, string perfil)
{
    servicioConfig.RestablecerRutaPorDefecto(perfil);
    var ruta = servicioConfig.AsegurarConfiguracionPerfil(perfil);
    Console.WriteLine($"Perfil '{perfil}' restaurado a la ubicación por defecto:");
    Console.WriteLine(ruta);
    return 0;
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

    const int segundosGracia = 90;
    var limite = DateTime.UtcNow.AddSeconds(segundosGracia);
    while (DateTime.UtcNow < limite)
    {
        await Task.Delay(500).ConfigureAwait(false);
        if (!ipc.EstaDemonioEnEjecucion(perfil))
        {
            Console.WriteLine("Demonio detenido correctamente.");
            await ipc.LimpiarComandoAsync(perfil).ConfigureAwait(false);
            return 0;
        }
    }

    Console.WriteLine("Timeout en apagado ordenado. Forzando cierre del proceso...");
    if (!ipc.TerminarDemonioForzadamente(perfil))
    {
        return Error("No se pudo detener el demonio. Termínalo manualmente desde el Administrador de tareas.");
    }

    await ipc.LimpiarComandoAsync(perfil).ConfigureAwait(false);
    Console.WriteLine("Demonio detenido (cierre forzado).");
    return 0;
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

static int EjecutarGestionPerfil(ServicioIpc ipc, string perfil, string[] args)
{
    var subcomando = args.Length > 1 ? args[1].ToLowerInvariant() : string.Empty;
    var eliminarJson = args.Any(a => a.Equals("-eliminarjsonpersonalizado", StringComparison.OrdinalIgnoreCase)
        || a.Equals("-eliminar-json", StringComparison.OrdinalIgnoreCase));

    return subcomando switch
    {
        "eliminar" or "borrar" or "delete" => EjecutarEliminarPerfil(ipc, perfil, eliminarJson),
        _ => Error("Usa: smanager perfil eliminar [-Perfil <nombre>] [-EliminarJsonPersonalizado]")
    };
}

static int EjecutarEliminarPerfil(ServicioIpc ipc, string perfil, bool eliminarJsonPersonalizado)
{
    var resultado = ServicioEliminacionPerfil.Eliminar(perfil, ipc, eliminarJsonPersonalizado);
    if (!resultado.Exito)
    {
        return Error(resultado.MensajeError ?? "No se pudo eliminar el perfil.");
    }

    Console.WriteLine($"Perfil '{perfil}' eliminado.");
    foreach (var item in resultado.ElementosEliminados)
    {
        Console.WriteLine($"  - {item}");
    }

    foreach (var aviso in resultado.Advertencias)
    {
        Console.WriteLine($"  (aviso) {aviso}");
    }

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

static async Task<int> EjecutarDesbloquearBorradoAsync(ServicioIpc ipc, string perfil)
{
    var exito = ServicioAutenticacionAdmin.SolicitarYValidarCredencialesAdminNativas(IntPtr.Zero, out var mensaje);
    if (!exito)
    {
        return Error(mensaje ?? "Autenticación de administrador local denegada.");
    }

    if (ipc.EstaDemonioEnEjecucion(perfil))
    {
        await ipc.EnviarComandoAsync(perfil, ComandoControl.DesbloquearBorrado, desbloquearBorrado: true).ConfigureAwait(false);
        Console.WriteLine($"[OK] {mensaje} (Señal de desbloqueo enviada al demonio).");
    }
    else
    {
        Console.WriteLine($"[OK] {mensaje} (El demonio no está en ejecución actualmente).");
    }

    return 0;
}

static int EjecutarListarPares(ServicioConfiguracionGui servicioConfig, string perfil)
{
    var ruta = servicioConfig.ResolverRutaConfiguracion(perfil);
    if (!File.Exists(ruta))
    {
        return Error($"No se encontró la configuración para el perfil '{perfil}' en {ruta}");
    }

    var config = servicioConfig.Cargar(ruta);
    Console.WriteLine($"Perfil: {perfil} | Total pares: {config.Pares.Count}");
    Console.WriteLine(new string('-', 95));
    Console.WriteLine($"{"ID / Nombre",-25} | {"Estado",-10} | {"Modo",-22} | {"Origen -> Destino",-30}");
    Console.WriteLine(new string('-', 95));

    foreach (var par in config.Pares)
    {
        var estadoText = !par.Habilitado ? "Inactivo" : par.Pausado ? "Pausado" : "Activo";
        var modoText = par.Modo switch
        {
            SManager.Core.Modelos.ModoSincronizacion.AcumulativoConBorradoOrigen => "Borrado en Origen",
            SManager.Core.Modelos.ModoSincronizacion.Espejo => "Espejo (Mirror)",
            _ => "Acumulativo"
        };
        var rutaShort = $"{AcortarTexto(par.RutaOrigen, 14)} -> {AcortarTexto(par.RutaDestino, 14)}";
        var nombreShort = AcortarTexto(string.IsNullOrWhiteSpace(par.Nombre) ? par.IdPar : par.Nombre, 24);

        Console.WriteLine($"{nombreShort,-25} | {estadoText,-10} | {modoText,-22} | {rutaShort,-30}");
    }
    Console.WriteLine(new string('-', 95));
    return 0;
}

static async Task<int> EjecutarCambiarModoAsync(
    ServicioIpc ipc,
    ServicioConfiguracionGui servicioConfig,
    string perfil,
    string[] args)
{
    if (args.Length < 3)
    {
        return Error("Uso: smanager set-mode <idPar|nombre> <0|1|2 | acumulativo|borrado-origen|espejo> [-Perfil <nombre>]");
    }

    var identificador = args[1];
    var modoStr = args[2].ToLowerInvariant();

    SManager.Core.Modelos.ModoSincronizacion modoFinal;
    if (modoStr is "0" or "acumulativo" or "acumulativo-sin-borrado")
    {
        modoFinal = SManager.Core.Modelos.ModoSincronizacion.AcumulativoSinBorrado;
    }
    else if (modoStr is "1" or "borrado-origen" or "borradoorigen" or "borrado_origen")
    {
        modoFinal = SManager.Core.Modelos.ModoSincronizacion.AcumulativoConBorradoOrigen;
    }
    else if (modoStr is "2" or "espejo" or "mirror")
    {
        modoFinal = SManager.Core.Modelos.ModoSincronizacion.Espejo;
    }
    else
    {
        return Error($"Modo no válido: '{modoStr}'. Usa: acumulativo (0), borrado-origen (1) o espejo (2).");
    }

    var ruta = servicioConfig.ResolverRutaConfiguracion(perfil);
    if (!File.Exists(ruta))
    {
        return Error($"No existe el archivo de configuración para el perfil '{perfil}'.");
    }

    var config = servicioConfig.Cargar(ruta);
    var parTarget = config.Pares.FirstOrDefault(p =>
        string.Equals(p.IdPar, identificador, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(p.Nombre, identificador, StringComparison.OrdinalIgnoreCase));

    if (parTarget is null)
    {
        return Error($"No se encontró ningún par con ID o Nombre '{identificador}' en el perfil '{perfil}'.");
    }

    parTarget.Modo = modoFinal;
    parTarget.BorrarEnOrigen = modoFinal == SManager.Core.Modelos.ModoSincronizacion.AcumulativoConBorradoOrigen;

    servicioConfig.Guardar(ruta, config);
    Console.WriteLine($"Par '{parTarget.Nombre}' ({parTarget.IdPar}) actualizado al modo: {parTarget.Modo}.");

    if (ipc.EstaDemonioEnEjecucion(perfil))
    {
        await ipc.EnviarComandoAsync(perfil, ComandoControl.Recargar).ConfigureAwait(false);
        Console.WriteLine($"Señal RECARGAR enviada al demonio activo ({perfil}).");
    }

    return 0;
}

static async Task<int> EjecutarAutorizarPurgaAsync(
    ServicioIpc ipc,
    ServicioConfiguracionGui servicioConfig,
    string perfil,
    string[] args)
{
    if (args.Length < 2)
    {
        return Error("Uso: smanager autorizar-purga <idPar|nombre> [-Perfil <nombre>]");
    }

    var identificador = args[1];
    var ruta = servicioConfig.ResolverRutaConfiguracion(perfil);
    if (!File.Exists(ruta))
    {
        return Error($"No existe el archivo de configuración para el perfil '{perfil}'.");
    }

    var config = servicioConfig.Cargar(ruta);
    var parTarget = config.Pares.FirstOrDefault(p =>
        string.Equals(p.IdPar, identificador, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(p.Nombre, identificador, StringComparison.OrdinalIgnoreCase));

    if (parTarget is null)
    {
        return Error($"No se encontró ningún par con ID o Nombre '{identificador}' en el perfil '{perfil}'.");
    }

    if (!ipc.EstaDemonioEnEjecucion(perfil))
    {
        return Error($"El demonio para el perfil '{perfil}' no está en ejecución.");
    }

    await ipc.EnviarComandoAsync(
        perfil,
        ComandoControl.AutorizarPurgaEspejo,
        idsPares: [parTarget.IdPar]).ConfigureAwait(false);

    Console.WriteLine($"[OK] Purga masiva autorizada intencionadamente para el par '{parTarget.Nombre}' ({parTarget.IdPar}).");
    Console.WriteLine("El demonio ejecutará la eliminación de los archivos huérfanos en destino en la siguiente pasada.");
    return 0;
}

static string AcortarTexto(string texto, int max) =>
    string.IsNullOrWhiteSpace(texto) ? "—" : (texto.Length <= max ? texto : string.Concat(texto.AsSpan(0, max - 3), "..."));

static int Error(string mensaje)
{
    Console.Error.WriteLine($"ERROR: {mensaje}");
    return 1;
}
