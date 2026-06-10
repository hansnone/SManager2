namespace SManager.Gui.WinUI.Servicios;

/// <summary>
/// Punto central de textos de interfaz (Fase 4 — base para localización).
/// De momento solo español; evita mezclar cadenas sueltas en XAML y ViewModels.
/// </summary>
public static class ServicioTextosUi
{
    public const string IdiomaPredeterminado = "es";

    public const string EtiquetaIdiomaEspanol = "Español";

    public static IReadOnlyList<string> OpcionesIdiomaEtiqueta { get; } = [EtiquetaIdiomaEspanol];

    public static string CodigoDesdeEtiqueta(string? etiqueta) =>
        string.Equals(etiqueta, EtiquetaIdiomaEspanol, StringComparison.OrdinalIgnoreCase)
            ? IdiomaPredeterminado
            : IdiomaPredeterminado;

    public static string EtiquetaDesdeCodigo(string? codigo) =>
        string.Equals(codigo, IdiomaPredeterminado, StringComparison.OrdinalIgnoreCase)
            ? EtiquetaIdiomaEspanol
            : EtiquetaIdiomaEspanol;
}
