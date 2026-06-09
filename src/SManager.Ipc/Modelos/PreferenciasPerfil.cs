using System.Text.Json.Serialization;

namespace SManager.Ipc.Modelos;

/// <summary>Preferencias persistentes de un perfil (ruta JSON personalizada, etc.).</summary>
public sealed class PreferenciasPerfil
{
    /// <summary>
    /// Ruta absoluta a un configuracion.json fuera de Perfiles configuracion.
    /// Null o vacío = usar la ubicación por defecto del perfil.
    /// </summary>
    [JsonPropertyName("ruta_configuracion_personalizada")]
    public string? RutaConfiguracionPersonalizada { get; set; }
}
