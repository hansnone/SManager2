namespace SManager.Gui.WinUI.Models;

/// <summary>Datos que el asistente entrega al ViewModel principal al finalizar.</summary>
public sealed class ResultadoAsistenteOnboarding
{
    public ParFilaViewModel Par { get; init; } = new();

    public bool GuardarConfiguracion { get; init; } = true;

    public bool IniciarSincronizacion { get; init; }

    public bool MarcarAsistenteCompletado { get; init; } = true;
}
