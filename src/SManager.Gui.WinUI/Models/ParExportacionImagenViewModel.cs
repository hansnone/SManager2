namespace SManager.Gui.WinUI.Models;

/// <summary>Instantánea de un par funcionando para capturar como imagen (estilo tarjeta Sincronización).</summary>
public sealed class ParExportacionImagenViewModel
{
    public string Nombre { get; init; } = string.Empty;

    public string RutaOrigen { get; init; } = string.Empty;

    public string RutaDestino { get; init; } = string.Empty;

    public string TamanoDestinoTexto { get; init; } = "—";

    public string Estado { get; init; } = "OK";

    public int Copiados { get; init; }

    public int Errores { get; init; }

    /// <summary>Clave para el chip tematizado (pares funcionando → OK).</summary>
    public string ClaveEstadoChip => "OK";

    public string EtiquetaEstado => Estado;

    public string TextoCopiados => $"{Copiados:N0} copiados";

    public string TextoErrores => $"{Errores:N0} errores";

    public string TextoTamanoDestino => $"Tamaño destino: {TamanoDestinoTexto}";
}
