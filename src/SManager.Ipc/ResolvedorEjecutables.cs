namespace SManager.Ipc;

/// <summary>Localiza smanager.exe y SManager.Host.exe en desarrollo o despliegue.</summary>
public static class ResolvedorEjecutables
{
    public const string NombreSubcarpetaHerramientas = "herramientas";

    private static readonly string[] ConfiguracionesCompilacion = ["Debug", "Release"];

    // Layouts típicos de salida en proyectos .NET (WinForms, WinUI x64, etc.).
    private static readonly string[][] LayoutsBinDesarrollo =
    [
        ["bin", "{config}", "net8.0"],
        ["bin", "x64", "{config}", "net8.0"],
        ["bin", "x86", "{config}", "net8.0"],
    ];

    public static string ResolverRutaCli(string? directorioBase = null)
    {
        directorioBase ??= AppContext.BaseDirectory;
        return ResolverPrimeroExistente(directorioBase,
        [
            "smanager.exe",
            "SManager.Cli.exe"
        ], proyectoDesarrollo: "SManager.Cli") ?? Path.Combine(directorioBase, "smanager.exe");
    }

    public static string ResolverRutaHost(string? directorioBase = null)
    {
        directorioBase ??= AppContext.BaseDirectory;
        return ResolverPrimeroExistente(directorioBase,
        [
            "SManager.Host.exe"
        ], proyectoDesarrollo: "SManager.Host") ?? Path.Combine(directorioBase, "SManager.Host.exe");
    }

    public static string? ResolverDirectorioHerramientas(string? directorioBase = null)
    {
        directorioBase ??= AppContext.BaseDirectory;
        var rutaCli = ResolverRutaCli(directorioBase);
        if (File.Exists(rutaCli))
        {
            return Path.GetDirectoryName(rutaCli);
        }

        var rutaHost = ResolverRutaHost(directorioBase);
        if (File.Exists(rutaHost))
        {
            return Path.GetDirectoryName(rutaHost);
        }

        return null;
    }

    private static string? ResolverPrimeroExistente(
        string directorioBase,
        IReadOnlyList<string> nombresArchivo,
        string proyectoDesarrollo)
    {
        // 1) Desarrollo: salida de cada proyecto (no se bloquea al recompilar la GUI).
        foreach (var configuracion in ConfiguracionesCompilacion)
        {
            foreach (var nombre in nombresArchivo)
            {
                var enDesarrollo = BuscarEnSalidaDesarrollo(directorioBase, proyectoDesarrollo, configuracion, nombre);
                if (enDesarrollo is not null)
                {
                    return enDesarrollo;
                }
            }

            // smanager vive en Cli; Host en su propio proyecto.
            if (proyectoDesarrollo == "SManager.Cli"
                && nombresArchivo.Contains("SManager.Host.exe", StringComparer.OrdinalIgnoreCase))
            {
                var enHost = BuscarEnSalidaDesarrollo(directorioBase, "SManager.Host", configuracion, "SManager.Host.exe");
                if (enHost is not null)
                {
                    return enHost;
                }
            }
        }

        // 2) Despliegue Release: herramientas\ junto al ejecutable de la GUI.
        var carpetaHerramientas = Path.Combine(directorioBase, NombreSubcarpetaHerramientas);
        if (Directory.Exists(carpetaHerramientas))
        {
            foreach (var nombre in nombresArchivo)
            {
                var enHerramientas = Path.Combine(carpetaHerramientas, nombre);
                if (File.Exists(enHerramientas))
                {
                    return enHerramientas;
                }
            }
        }

        // 3) Legacy: mismo directorio que el ejecutable que lanzó la búsqueda.
        foreach (var nombre in nombresArchivo)
        {
            var junto = Path.Combine(directorioBase, nombre);
            if (File.Exists(junto))
            {
                return junto;
            }
        }

        return null;
    }

    /// <summary>
    /// Sube desde la carpeta de salida de la GUI hasta encontrar el directorio que contiene
    /// el proyecto destino (p. ej. src\ con SManager.Cli como hermano de SManager.Gui.WinUI).
    /// </summary>
    private static string? BuscarEnSalidaDesarrollo(
        string directorioBase,
        string proyecto,
        string configuracion,
        string nombreArchivo)
    {
        var directorioActual = Path.GetFullPath(directorioBase);
        for (var nivel = 0; nivel < 10 && !string.IsNullOrEmpty(directorioActual); nivel++)
        {
            var carpetaProyecto = Path.Combine(directorioActual, proyecto);
            if (Directory.Exists(carpetaProyecto))
            {
                foreach (var layout in LayoutsBinDesarrollo)
                {
                    var segmentos = layout.Select(s => s.Replace("{config}", configuracion, StringComparison.Ordinal)).ToArray();
                    var ruta = Path.Combine([carpetaProyecto, .. segmentos, nombreArchivo]);
                    if (File.Exists(ruta))
                    {
                        return ruta;
                    }
                }
            }

            directorioActual = Path.GetDirectoryName(directorioActual);
        }

        return null;
    }
}
