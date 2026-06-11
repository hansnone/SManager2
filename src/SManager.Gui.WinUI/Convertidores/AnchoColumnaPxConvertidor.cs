using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SManager.Gui.WinUI.Convertidores;

/// <summary>Convierte ancho en píxeles (ViewModel) a GridLength para ColumnDefinition.</summary>
public sealed class AnchoColumnaPxConvertidor : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is double pixeles && pixeles > 0
            ? new GridLength(pixeles)
            : new GridLength(120);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is GridLength longitud && longitud.GridUnitType == GridUnitType.Pixel
            ? longitud.Value
            : 120d;
}
