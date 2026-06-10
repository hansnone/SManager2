using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using SManager.Gui.WinUI.ViewModels;

namespace SManager.Gui.WinUI.Convertidores;

/// <summary>Muestra el bloque del paso activo del asistente de onboarding.</summary>
public sealed class PasoAsistenteVisibilidadConvertidor : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not PasoAsistenteOnboarding paso || parameter is not string nombrePaso)
        {
            return Visibility.Collapsed;
        }

        return Enum.TryParse<PasoAsistenteOnboarding>(nombrePaso, ignoreCase: true, out var esperado)
               && paso == esperado
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
