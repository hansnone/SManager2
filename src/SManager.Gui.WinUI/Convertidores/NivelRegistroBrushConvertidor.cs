using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace SManager.Gui.WinUI.Convertidores;

/// <summary>Resuelve pinceles del registro según nivel y tema activo (claro/oscuro).</summary>
public sealed class NivelRegistroBrushConvertidor : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var nivel = value as string ?? "INFO";
        var rol = parameter as string ?? "Fondo";
        var clave = ObtenerClaveRecurso(nivel, rol);
        var recursos = Application.Current.Resources;

        if (recursos.TryGetValue(clave, out var pincel) && pincel is SolidColorBrush)
        {
            return pincel;
        }

        return recursos.TryGetValue("RegistroInfoFondo", out var fallback)
            ? fallback
            : new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();

    private static string ObtenerClaveRecurso(string nivel, string rol)
    {
        var prefijo = nivel.ToUpperInvariant() switch
        {
            "ERROR" => "RegistroError",
            "WARN" or "WARNING" => "RegistroWarn",
            "PENDIENTE" => "RegistroPendiente",
            _ => "RegistroInfo"
        };

        var sufijo = rol.ToUpperInvariant() switch
        {
            "NIVEL" => "Nivel",
            "MENSAJE" => "Mensaje",
            _ => "Fondo"
        };

        return prefijo + sufijo;
    }
}
