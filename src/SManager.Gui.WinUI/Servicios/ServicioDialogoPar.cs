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
        return resultado == ContentDialogResult.Primary ? formulario.ObtenerPar() : null;
    }
}
