using SManager.Core.Configuracion;
using SManager.Core.Modelos;
using SManager.Ipc;

namespace SManager.Gui.Shared;

/// <summary>
/// Persistencia del JSON de configuración.
/// Por defecto: Perfiles configuracion\{perfil}\configuracion.json.
/// Opcional: ruta personalizada por perfil (ver ResolvedorRutasConfiguracion).
/// </summary>
public sealed class ServicioConfiguracionGui
{
    private readonly ConfiguracionRepositorio _repositorio = new();

    public string ObtenerRutaPorDefecto(string perfil) =>
        ResolvedorRutasConfiguracion.ObtenerRutaPorDefecto(perfil);

    public string ResolverRutaConfiguracion(string perfil) =>
        ResolvedorRutasConfiguracion.ResolverRuta(perfil);

    public bool UsaRutaPersonalizada(string perfil) =>
        ResolvedorRutasConfiguracion.UsaRutaPersonalizada(perfil);

    public void EstablecerRutaPersonalizada(string perfil, string rutaJson) =>
        ResolvedorRutasConfiguracion.EstablecerRutaPersonalizada(perfil, rutaJson);

    public void RestablecerRutaPorDefecto(string perfil) =>
        ResolvedorRutasConfiguracion.RestablecerRutaPorDefecto(perfil);

    public IReadOnlyList<string> ListarPerfiles() => RutasDatos.ListarNombresPerfiles();

    /// <summary>Asegura carpetas del perfil y el JSON en la ruta resuelta (por defecto o personalizada).</summary>
    public string AsegurarConfiguracionPerfil(string nombrePerfil)
    {
        ValidarNombrePerfil(nombrePerfil);
        Directory.CreateDirectory(RutasDatos.ObtenerCarpetaPerfil(nombrePerfil));

        var ruta = ResolverRutaConfiguracion(nombrePerfil);
        if (!File.Exists(ruta))
        {
            var carpeta = Path.GetDirectoryName(ruta);
            if (!string.IsNullOrEmpty(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }

            Guardar(ruta, CrearPorDefecto());
        }

        return ruta;
    }

    /// <summary>Crea carpeta del perfil y configuracion.json si no existen.</summary>
    public string CrearPerfil(string nombrePerfil) => AsegurarConfiguracionPerfil(nombrePerfil);

    public ConfiguracionAplicacion Cargar(string ruta)
    {
        if (!File.Exists(ruta))
        {
            return CrearPorDefecto();
        }

        return _repositorio.LeerAsync(ruta).GetAwaiter().GetResult();
    }

    public void Guardar(string ruta, ConfiguracionAplicacion config)
    {
        _repositorio.GuardarAsync(ruta, config).GetAwaiter().GetResult();
    }

    public static ConfiguracionAplicacion CrearPorDefecto() => new()
    {
        Pares =
        [
            new ParSincronizacion
            {
                Nombre = "Nuevo par",
                FiltroExclusion = "~$*;*.tmp;*.partial;*.lnk"
            }
        ]
    };

    public static void ValidarNombrePerfil(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException("El nombre del perfil no puede estar vacío.");
        }

        if (nombre.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("El nombre contiene caracteres no válidos para una carpeta.");
        }
    }
}
