using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SManager.Gui.WinUI.Convertidores;

/// <summary>Visible cuando el texto no está vacío (avisos, pistas).</summary>
public sealed class TextoNoVacioAVisibilidadConvertidor : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is string texto && !string.IsNullOrWhiteSpace(texto)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
