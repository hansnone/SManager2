using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SManager.Gui.WinUI.Models;
using SManager.Gui.WinUI.Vistas;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Muestra el diálogo modal para crear o editar un par de sincronización.</summary>
public static class ServicioDialogoPar
{
    /// <summary>Devuelve el par editado o null si el usuario cancela.</summary>
    public static async Task<ParFilaViewModel?> MostrarAsync(ParFilaViewModel? parExistente, bool esEdicion)
    {
        var formulario = new DialogoParControl();
        formulario.Cargar(parExistente, esEdicion);

        var cuadro = new ContentDialog
        {
            Title = esEdicion ? "Editar par" : "Nuevo par",
            PrimaryButtonText = esEdicion ? "Guardar cambios" : "Añadir",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            Content = formulario,
            XamlRoot = App.Window.Content.XamlRoot,
            IsPrimaryButtonEnabled = formulario.EsValido()
        };

        formulario.ValidezCambiada += (_, _) =>
            cuadro.IsPrimaryButtonEnabled = formulario.EsValido();

        var resultado = await cuadro.ShowAsync();
        if (resultado != ContentDialogResult.Primary)
        {
            return null;
        }

        var parNuevo = formulario.ObtenerPar();
        if (esEdicion
            && parExistente is not null
            && RutasParCambiaron(parExistente, parNuevo)
            && !await ConfirmarCambioRutasAsync(parExistente, parNuevo))
        {
            return null;
        }

        return parNuevo;
    }

    /// <summary>Pregunta antes de cambiar origen o destino en un par existente.</summary>
    private static async Task<bool> ConfirmarCambioRutasAsync(
        ParFilaViewModel anterior,
        ParFilaViewModel nuevo)
    {
        var cambios = new List<string>();
        if (!string.Equals(anterior.RutaOrigen, nuevo.RutaOrigen, StringComparison.OrdinalIgnoreCase))
        {
            cambios.Add($"Origen:\n  Antes: {anterior.RutaOrigen}\n  Ahora: {nuevo.RutaOrigen}");
        }

        if (!string.Equals(anterior.RutaDestino, nuevo.RutaDestino, StringComparison.OrdinalIgnoreCase))
        {
            cambios.Add($"Destino:\n  Antes: {anterior.RutaDestino}\n  Ahora: {nuevo.RutaDestino}");
        }

        var cuadro = new ContentDialog
        {
            Title = "¿Cambiar carpetas del par?",
            Content = new TextBlock
            {
                Text =
                    "Estás modificando rutas de un par ya configurado. La próxima sincronización usará las carpetas nuevas.\n\n"
                    + string.Join("\n\n", cambios),
                TextWrapping = TextWrapping.WrapWholeWords
            },
            PrimaryButtonText = "Sí, cambiar rutas",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.Window.Content.XamlRoot
        };

        return await cuadro.ShowAsync() == ContentDialogResult.Primary;
    }

    private static bool RutasParCambiaron(ParFilaViewModel anterior, ParFilaViewModel nuevo) =>
        !string.Equals(anterior.RutaOrigen, nuevo.RutaOrigen, StringComparison.OrdinalIgnoreCase)
        || !string.Equals(anterior.RutaDestino, nuevo.RutaDestino, StringComparison.OrdinalIgnoreCase);
}
