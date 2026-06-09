using System.Text.Json;
using SManager.Ipc.Modelos;

namespace SManager.Ipc;

/// <summary>
/// Resuelve la ruta del JSON de configuración por perfil:
/// por defecto en %LOCALAPPDATA%\SManager2\Perfiles configuracion\{perfil}\configuracion.json
/// o una ruta personalizada guardada en preferencias del perfil.
/// </summary>
public static class ResolvedorRutasConfiguracion
{
    public const string NombreArchivoPreferencias = "preferencias.json";

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>Ubicación estándar editable por la GUI para el perfil.</summary>
    public static string ObtenerRutaPorDefecto(string nombrePerfil) =>
        RutasDatos.ResolverRutaConfiguracionUsuario(nombrePerfil);

    /// <summary>Preferencias del perfil (enlace a JSON personalizado).</summary>
    public static string ResolverRutaArchivoPreferencias(string nombrePerfil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombrePerfil);
        return Path.Combine(RutasDatos.ResolverCarpetaPerfilIpc(nombrePerfil), NombreArchivoPreferencias);
    }

    /// <summary>Crea la carpeta IPC si hace falta y devuelve la ruta de preferencias.</summary>
    public static string ObtenerRutaArchivoPreferencias(string nombrePerfil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombrePerfil);
        return Path.Combine(RutasDatos.ObtenerCarpetaPerfil(nombrePerfil), NombreArchivoPreferencias);
    }

    /// <summary>Ruta personalizada registrada, o null si usa la por defecto.</summary>
    public static string? ObtenerRutaPersonalizada(string nombrePerfil)
    {
        var preferencias = LeerPreferencias(nombrePerfil);
        var ruta = preferencias?.RutaConfiguracionPersonalizada?.Trim();
        return string.IsNullOrWhiteSpace(ruta) ? null : Path.GetFullPath(ruta);
    }

    public static bool UsaRutaPersonalizada(string nombrePerfil) =>
        ObtenerRutaPersonalizada(nombrePerfil) is not null;

    /// <summary>Ruta efectiva: personalizada si está definida; si no, la por defecto.</summary>
    public static string ResolverRuta(string nombrePerfil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombrePerfil);
        var personalizada = ObtenerRutaPersonalizada(nombrePerfil);
        return personalizada ?? ObtenerRutaPorDefecto(nombrePerfil);
    }

    /// <summary>Registra una ruta JSON personalizada para el perfil.</summary>
    public static void EstablecerRutaPersonalizada(string nombrePerfil, string rutaJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombrePerfil);
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaJson);

        var rutaCompleta = Path.GetFullPath(rutaJson);
        if (!rutaCompleta.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("La configuración debe ser un archivo .json", nameof(rutaJson));
        }

        GuardarPreferencias(nombrePerfil, new PreferenciasPerfil
        {
            RutaConfiguracionPersonalizada = rutaCompleta
        });
    }

    /// <summary>Vuelve a la ubicación por defecto del perfil.</summary>
    public static void RestablecerRutaPorDefecto(string nombrePerfil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombrePerfil);
        var rutaPrefs = ResolverRutaArchivoPreferencias(nombrePerfil);
        if (File.Exists(rutaPrefs))
        {
            File.Delete(rutaPrefs);
        }
    }

    private static PreferenciasPerfil? LeerPreferencias(string nombrePerfil)
    {
        var ruta = ResolverRutaArchivoPreferencias(nombrePerfil);
        if (!File.Exists(ruta))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(ruta);
            return JsonSerializer.Deserialize<PreferenciasPerfil>(json, OpcionesJson);
        }
        catch
        {
            return null;
        }
    }

    private static void GuardarPreferencias(string nombrePerfil, PreferenciasPerfil preferencias)
    {
        var ruta = ObtenerRutaArchivoPreferencias(nombrePerfil);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        var json = JsonSerializer.Serialize(preferencias, OpcionesJson);
        EscrituraAtomica.EscribirTextoAsync(ruta, json).GetAwaiter().GetResult();
    }
}
