using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SManager.Gui.WinUI.Convertidores;

/// <summary>Convierte bool a Visibility; parámetro "Invertir" invierte el resultado.</summary>
public sealed class BooleanoAVisibilidadConvertidor : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var visible = value is bool activo && activo;
        if (parameter is string parametro
            && parametro.Equals("Invertir", StringComparison.OrdinalIgnoreCase))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
