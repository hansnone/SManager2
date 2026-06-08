using System.Text.Json.Serialization;

namespace SManager.Ipc.Modelos;

/// <summary>Señal escrita por CLI/GUI y consumida por el demonio.</summary>
public sealed class ControlPerfil
{
    [JsonPropertyName("comando")]
    public string Comando { get; set; } = string.Empty;

    [JsonPropertyName("emitido_utc")]
    public string EmitidoUtc { get; set; } = string.Empty;
}
