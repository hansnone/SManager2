using Microsoft.UI.Xaml;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Aplica tema claro, oscuro o el del sistema a la ventana principal.</summary>
public static class ServicioTemaAplicacion
{
    public const string TemaSistema = "Sistema";
    public const string TemaClaro = "Claro";
    public const string TemaOscuro = "Oscuro";

    public static IReadOnlyList<string> Opciones { get; } =
        [TemaSistema, TemaClaro, TemaOscuro];

    /// <summary>Normaliza valores antiguos o desconocidos al tema del sistema.</summary>
    public static string Normalizar(string? tema) =>
        tema switch
        {
            TemaClaro => TemaClaro,
            TemaOscuro => TemaOscuro,
            _ => TemaSistema
        };

    /// <summary>Aplica el tema al contenido de la ventana (respeta Light/Dark de Windows si es Sistema).</summary>
    public static void Aplicar(string? tema, FrameworkElement? raiz = null)
    {
        var elemento = raiz ?? App.Window?.Content as FrameworkElement;
        if (elemento is null)
        {
            return;
        }

        elemento.RequestedTheme = Normalizar(tema) switch
        {
            TemaClaro => ElementTheme.Light,
            TemaOscuro => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }
}
