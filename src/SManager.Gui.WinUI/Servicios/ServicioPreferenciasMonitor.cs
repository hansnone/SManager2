using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SManager.Ipc;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>
/// Persiste las alturas de los paneles del Monitor en un JSON bajo %LOCALAPPDATA%\SManager2.
/// No usa ApplicationData (falla en apps unpackaged sin identidad de paquete).
/// </summary>
public static class ServicioPreferenciasMonitor
{
    private static readonly JsonSerializerOptions OpcionesJson = new() { WriteIndented = true };

    private static string RutaArchivo =>
        Path.Combine(RutasDatos.ResolverRaiz(), "preferencias_monitor.json");

    /// <summary>Restaura proporciones guardadas si el fichero existe.</summary>
    public static void RestaurarSiExiste(
        RowDefinition filaPares,
        RowDefinition filaCopias,
        RowDefinition filaActividad)
    {
        try
        {
            if (!File.Exists(RutaArchivo))
            {
                return;
            }

            var json = File.ReadAllText(RutaArchivo);
            var datos = JsonSerializer.Deserialize<PreferenciasMonitorDto>(json, OpcionesJson);
            if (datos is null)
            {
                return;
            }

            if (TryLeerGridLength(datos.Pares, out var altoPares))
            {
                filaPares.Height = altoPares;
            }

            if (TryLeerGridLength(datos.Copias, out var altoCopias))
            {
                filaCopias.Height = altoCopias;
            }

            if (TryLeerGridLength(datos.Actividad, out var altoActividad))
            {
                filaActividad.Height = altoActividad;
            }
        }
        catch
        {
            // Preferencias corruptas o sin permisos: usar valores por defecto del XAML.
        }
    }

    /// <summary>Guarda las alturas actuales tras arrastrar un separador.</summary>
    public static void Guardar(
        RowDefinition filaPares,
        RowDefinition filaCopias,
        RowDefinition filaActividad)
    {
        try
        {
            Directory.CreateDirectory(RutasDatos.ResolverRaiz());
            var datos = new PreferenciasMonitorDto(
                SerializarGridLength(filaPares.Height),
                SerializarGridLength(filaCopias.Height),
                SerializarGridLength(filaActividad.Height));

            File.WriteAllText(RutaArchivo, JsonSerializer.Serialize(datos, OpcionesJson));
        }
        catch
        {
            // No bloquear la UI si no se puede escribir el fichero.
        }
    }

    private sealed record PreferenciasMonitorDto(string Pares, string Copias, string Actividad);

    private static string SerializarGridLength(GridLength longitud) =>
        longitud.GridUnitType switch
        {
            GridUnitType.Star => $"{longitud.Value:0.####}*",
            GridUnitType.Pixel => $"{longitud.Value:0.####}px",
            _ => "1*"
        };

    private static bool TryLeerGridLength(string? texto, out GridLength longitud)
    {
        longitud = default;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        texto = texto.Trim();
        if (texto.EndsWith('*'))
        {
            if (double.TryParse(texto[..^1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var estrellas))
            {
                longitud = new GridLength(estrellas, GridUnitType.Star);
                return true;
            }

            return false;
        }

        if (texto.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            texto = texto[..^2];
        }

        if (double.TryParse(texto, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var pixeles))
        {
            longitud = new GridLength(pixeles, GridUnitType.Pixel);
            return true;
        }

        return false;
    }
}
