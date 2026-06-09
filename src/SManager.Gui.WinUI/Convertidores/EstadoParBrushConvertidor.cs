using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace SManager.Gui.WinUI.Convertidores;

/// <summary>Resuelve pinceles de chips de estado de par según clave y tema activo.</summary>
public sealed class EstadoParBrushConvertidor : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var estado = value as string ?? "OK";
        var rol = parameter as string ?? "Fondo";
        var clave = ObtenerClaveRecurso(estado, rol);
        var recursos = Application.Current.Resources;

        if (recursos.TryGetValue(clave, out var pincel) && pincel is SolidColorBrush)
        {
            return pincel;
        }

        var claveNeutral = rol.Equals("Texto", StringComparison.OrdinalIgnoreCase)
            ? "EstadoNeutralTexto"
            : "EstadoNeutralFondo";

        return recursos.TryGetValue(claveNeutral, out var fallback)
            ? fallback
            : new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();

    private static string ObtenerClaveRecurso(string estado, string rol)
    {
        var prefijo = estado.ToUpperInvariant() switch
        {
            "OK" or "ACTIVO" => "EstadoOk",
            "PAUSADO" => "EstadoPausado",
            "ERROR" => "EstadoError",
            "INACTIVO" => "EstadoInactivo",
            _ => "EstadoNeutral"
        };

        var sufijo = rol.Equals("Texto", StringComparison.OrdinalIgnoreCase) ? "Texto" : "Fondo";
        return prefijo + sufijo;
    }
}
