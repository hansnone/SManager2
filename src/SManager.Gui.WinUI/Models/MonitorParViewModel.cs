namespace SManager.Gui.WinUI.Models;

public sealed class MonitorParViewModel
{
    public string Nombre { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public int Copiados { get; init; }
    public int Errores { get; init; }
}
