using System.Text.Json;
using SManager.Ipc;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Anchos de columnas de Registro y Monitor persistidos en disco.</summary>
public sealed class PreferenciasColumnasDto
{
    public double RegistroHora { get; set; } = 140;

    public double RegistroPar { get; set; } = 120;

    public double RegistroNivel { get; set; } = 72;

    public double RegistroMensaje { get; set; } = 360;

    public double MonitorNombre { get; set; } = 160;

    public double MonitorEstado { get; set; } = 100;

    public double MonitorTamanoDestino { get; set; } = 120;

    public double MonitorCopiados { get; set; } = 88;

    public double MonitorErrores { get; set; } = 72;
}

/// <summary>Lee y guarda preferencias de columnas redimensionables.</summary>
public static class ServicioPreferenciasColumnas
{
    private static readonly JsonSerializerOptions OpcionesJson = new() { WriteIndented = true };

    private static string RutaArchivo =>
        Path.Combine(RutasDatos.ResolverRaiz(), "preferencias_columnas.json");

    public static PreferenciasColumnasDto Cargar()
    {
        try
        {
            if (!File.Exists(RutaArchivo))
            {
                return new PreferenciasColumnasDto();
            }

            var json = File.ReadAllText(RutaArchivo);
            return JsonSerializer.Deserialize<PreferenciasColumnasDto>(json, OpcionesJson)
                ?? new PreferenciasColumnasDto();
        }
        catch
        {
            return new PreferenciasColumnasDto();
        }
    }

    public static void Guardar(PreferenciasColumnasDto preferencias)
    {
        try
        {
            Directory.CreateDirectory(RutasDatos.ResolverRaiz());
            var json = JsonSerializer.Serialize(preferencias, OpcionesJson);
            File.WriteAllText(RutaArchivo, json);
        }
        catch
        {
            // No bloquear la GUI si falla la escritura.
        }
    }
}
