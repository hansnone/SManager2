namespace SManager.Core.Motor;

/// <summary>Parámetros de arranque del motor para un perfil.</summary>
public sealed class OpcionesDemonio
{
    public required string NombrePerfil { get; init; }
    public required string RutaConfiguracion { get; init; }
}
