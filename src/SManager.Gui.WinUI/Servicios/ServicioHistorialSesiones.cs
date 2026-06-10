using System.Text.Json;
using SManager.Gui.WinUI.Models;
using SManager.Ipc;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Entrada persistida de una sesión de sincronización finalizada.</summary>
public sealed class EntradaHistorialSesionDto
{
    public string InicioUtc { get; set; } = string.Empty;

    public string FinUtc { get; set; } = string.Empty;

    public int Copiados { get; set; }

    public int Errores { get; set; }

    public long BytesEscritos { get; set; }
}

/// <summary>Historial local por perfil (últimas sesiones del demonio).</summary>
public sealed class ArchivoHistorialSesionesDto
{
    public List<EntradaHistorialSesionDto> Entradas { get; set; } = [];
}

/// <summary>Persiste y consulta el historial de sesiones en la carpeta IPC del perfil.</summary>
public static class ServicioHistorialSesiones
{
    private const int MaxEntradas = 20;

    private static readonly JsonSerializerOptions OpcionesJson = new() { WriteIndented = true };

    /// <summary>Registra una sesión al detener el demonio.</summary>
    public static void RegistrarSesionFinalizada(
        string nombrePerfil,
        DateTimeOffset inicioUtc,
        DateTimeOffset finUtc,
        int copiados,
        int errores,
        long bytesEscritos)
    {
        if (string.IsNullOrWhiteSpace(nombrePerfil))
        {
            return;
        }

        try
        {
            var archivo = CargarArchivo(nombrePerfil);
            archivo.Entradas.Insert(
                0,
                new EntradaHistorialSesionDto
                {
                    InicioUtc = inicioUtc.ToString("o"),
                    FinUtc = finUtc.ToString("o"),
                    Copiados = copiados,
                    Errores = errores,
                    BytesEscritos = bytesEscritos
                });

            if (archivo.Entradas.Count > MaxEntradas)
            {
                archivo.Entradas.RemoveRange(MaxEntradas, archivo.Entradas.Count - MaxEntradas);
            }

            GuardarArchivo(nombrePerfil, archivo);
        }
        catch
        {
            // El historial es informativo; no debe bloquear la GUI.
        }
    }

    /// <summary>Devuelve entradas recientes como ViewModels listos para la UI.</summary>
    public static IReadOnlyList<HistorialSesionViewModel> CargarRecientes(string nombrePerfil)
    {
        try
        {
            var archivo = CargarArchivo(nombrePerfil);
            return archivo.Entradas
                .Select(HistorialSesionViewModel.DesdeDto)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Última sesión sin errores, si existe.</summary>
    public static HistorialSesionViewModel? ObtenerUltimaSesionCorrecta(string nombrePerfil)
    {
        try
        {
            return CargarArchivo(nombrePerfil).Entradas
                .FirstOrDefault(e => e.Errores == 0) is { } dto
                ? HistorialSesionViewModel.DesdeDto(dto)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolverRuta(string nombrePerfil) =>
        Path.Combine(RutasDatos.ResolverCarpetaPerfilIpc(nombrePerfil), "historial_sesiones.json");

    private static ArchivoHistorialSesionesDto CargarArchivo(string nombrePerfil)
    {
        var ruta = ResolverRuta(nombrePerfil);
        if (!File.Exists(ruta))
        {
            return new ArchivoHistorialSesionesDto();
        }

        var json = File.ReadAllText(ruta);
        return JsonSerializer.Deserialize<ArchivoHistorialSesionesDto>(json, OpcionesJson)
            ?? new ArchivoHistorialSesionesDto();
    }

    private static void GuardarArchivo(string nombrePerfil, ArchivoHistorialSesionesDto archivo)
    {
        var ruta = ResolverRuta(nombrePerfil);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        var json = JsonSerializer.Serialize(archivo, OpcionesJson);
        File.WriteAllText(ruta, json);
    }
}
