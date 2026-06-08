namespace SManager.Host;

/// <summary>Modos de ejecución del ejecutable Host.</summary>
public sealed class OpcionesArranque
{
    /// <summary>Windows Service: supervisor de perfiles del usuario.</summary>
    public bool ModoServicio { get; init; }

    /// <summary>Demonio oculto para un único perfil (lanzado por CLI start).</summary>
    public bool ModoDemonio { get; init; }

    public string Perfil { get; init; } = "General";
    public string? RutaConfiguracion { get; init; }
}
