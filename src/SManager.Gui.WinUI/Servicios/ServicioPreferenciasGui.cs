using System.Text.Json;
using SManager.Ipc;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Preferencias de la GUI no ligadas a un perfil (auto-arranque, etc.).</summary>
public sealed class PreferenciasGuiDto
{
    public bool AutoInicioHabilitado { get; set; }

    public bool AutoInicioMinimizado { get; set; } = true;
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
