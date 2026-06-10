using System.Text.Json;
using SManager.Ipc;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Preferencias de la GUI no ligadas a un perfil (auto-arranque, etc.).</summary>
public sealed class PreferenciasGuiDto
{
    public bool AutoInicioHabilitado { get; set; }

    public bool AutoInicioMinimizado { get; set; } = true;

    /// <summary>Al arrancar Windows, iniciar también el demonio de sincronización.</summary>
    public bool AutoInicioIniciarDemonio { get; set; } = true;

    /// <summary>True tras completar u omitir el asistente de primer par.</summary>
    public bool AsistenteCompletado { get; set; }

    /// <summary>Si es false, no se muestran TeachingTips automáticos.</summary>
    public bool MostrarConsejosContextuales { get; set; } = true;

    /// <summary>Identificadores de consejos contextuales ya mostrados al usuario.</summary>
    public List<string> ConsejosVistos { get; set; } = [];

    /// <summary>Al cerrar la ventana, ocultar en la bandeja en lugar de salir.</summary>
    public bool MinimizarABandejaAlCerrar { get; set; } = true;

    /// <summary>Mostrar toasts cuando cambia el estado de sincronización.</summary>
    public bool NotificacionesHabilitadas { get; set; } = true;

    /// <summary>Mostrar icono en la bandeja del sistema.</summary>
    public bool BandejaHabilitada { get; set; } = true;

    /// <summary>Si es false, oculta secciones técnicas (Monitor, Registro, Estadísticas avanzadas).</summary>
    public bool ModoInterfazAvanzado { get; set; }

    /// <summary>Tema de la GUI: Sistema, Claro u Oscuro.</summary>
    public string TemaAplicacion { get; set; } = ServicioTemaAplicacion.TemaSistema;

    /// <summary>Código de idioma de la interfaz (preparado para localización futura).</summary>
    public string IdiomaUi { get; set; } = "es";
}

/// <summary>Persiste preferencias globales de la GUI en %LOCALAPPDATA%\SManager2.</summary>
public static class ServicioPreferenciasGui
{
    private static readonly JsonSerializerOptions OpcionesJson = new() { WriteIndented = true };

    private static string RutaArchivo =>
        Path.Combine(RutasDatos.ResolverRaiz(), "preferencias_gui.json");

    public static PreferenciasGuiDto Cargar()
    {
        try
        {
            if (!File.Exists(RutaArchivo))
            {
                return new PreferenciasGuiDto();
            }

            var json = File.ReadAllText(RutaArchivo);
            return JsonSerializer.Deserialize<PreferenciasGuiDto>(json, OpcionesJson)
                ?? new PreferenciasGuiDto();
        }
        catch
        {
            return new PreferenciasGuiDto();
        }
    }

    public static void Guardar(PreferenciasGuiDto preferencias)
    {
        Directory.CreateDirectory(RutasDatos.ResolverRaiz());
        var json = JsonSerializer.Serialize(preferencias, OpcionesJson);
        File.WriteAllText(RutaArchivo, json);
    }
}
