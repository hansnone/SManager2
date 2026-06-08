using SManager.Core.Configuracion;
using SManager.Core.Modelos;
using SManager.Ipc;

namespace SManager.Gui.Shared;

/// <summary>
/// Persistencia del JSON en Perfiles configuracion\{perfil}\configuracion.json.
/// Crea la carpeta y un JSON por defecto si el perfil es nuevo.
/// </summary>
public sealed class ServicioConfiguracionGui
{
    private readonly ConfiguracionRepositorio _repositorio = new();

    public string ObtenerRutaPorDefecto(string perfil) =>
        RutasDatos.ObtenerRutaConfiguracionUsuario(perfil);

    public IReadOnlyList<string> ListarPerfiles() => RutasDatos.ListarNombresPerfiles();

    /// <summary>Crea carpeta del perfil y configuracion.json si no existen.</summary>
    public string CrearPerfil(string nombrePerfil)
    {
        ValidarNombrePerfil(nombrePerfil);
        var ruta = ObtenerRutaPorDefecto(nombrePerfil);
        if (!File.Exists(ruta))
        {
            Guardar(ruta, CrearPorDefecto());
        }

        Directory.CreateDirectory(RutasDatos.ObtenerCarpetaPerfil(nombrePerfil));
        return ruta;
    }

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
