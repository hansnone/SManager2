using System.Text.Json.Serialization;

namespace SManager.Ipc.Modelos;

/// <summary>Señal escrita por CLI/GUI y consumida por el demonio.</summary>
public sealed class ControlPerfil
{
    [JsonPropertyName("comando")]
    public string Comando { get; set; } = string.Empty;

    [JsonPropertyName("ids_pares")]
    public List<string>? IdsPares { get; set; }

    [JsonPropertyName("desbloquear_borrado")]
    public bool? DesbloquearBorrado { get; set; }

    [JsonPropertyName("emitido_utc")]
    public string EmitidoUtc { get; set; } = string.Empty;
}
