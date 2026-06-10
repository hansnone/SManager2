using Microsoft.UI;
using Microsoft.Windows.Storage.Pickers;
using WinRT.Interop;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Selector de carpetas nativo de Windows enlazado a la ventana WinUI.</summary>
public static class ServicioSelectorCarpeta
{
    /// <summary>
    /// Abre el diálogo de carpetas. Debe invocarse desde el hilo UI.
    /// Con <paramref name="desdeDialogoModal"/> usa FolderBrowserDialog (WinForms), que funciona encima de un ContentDialog.
    /// </summary>
    public static async Task<string?> ElegirCarpetaAsync(
        string? rutaSugerida = null,
        bool desdeDialogoModal = false)
    {
        if (!App.DispatcherQueue.HasThreadAccess)
        {
            var completado = new TaskCompletionSource<string?>();
            App.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    completado.SetResult(await ElegirCarpetaEnHiloUiAsync(rutaSugerida, desdeDialogoModal));
                }
                catch (Exception ex)
                {
                    completado.SetException(ex);
                }
            });
            return await completado.Task;
        }

        return await ElegirCarpetaEnHiloUiAsync(rutaSugerida, desdeDialogoModal);
    }

    private static Task<string?> ElegirCarpetaEnHiloUiAsync(string? rutaSugerida, bool desdeDialogoModal)
    {
        if (desdeDialogoModal)
        {
            return Task.FromResult(ElegirConDialogoWinForms(rutaSugerida));
        }

        return ElegirConPickerWinUiAsync(rutaSugerida);
    }

    /// <summary>FolderPicker de WinUI; no se muestra correctamente encima de un ContentDialog modal.</summary>
    private static async Task<string?> ElegirConPickerWinUiAsync(string? rutaSugerida)
    {
        App.Window.AppWindow.Show();

        var hwnd = WindowNative.GetWindowHandle(App.Window);
        var idVentana = Win32Interop.GetWindowIdFromWindow(hwnd);

        var selector = new FolderPicker(idVentana)
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            ViewMode = PickerViewMode.List,
            Title = "Seleccionar carpeta"
        };

        if (!string.IsNullOrWhiteSpace(rutaSugerida) && Directory.Exists(rutaSugerida))
        {
            selector.SuggestedStartFolder = rutaSugerida;
        }

        var resultado = await selector.PickSingleFolderAsync();
        return resultado?.Path;
    }

    /// <summary>Diálogo clásico de Windows; fiable dentro de ContentDialog.</summary>
    private static string? ElegirConDialogoWinForms(string? rutaSugerida)
    {
        using var dialogo = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Seleccionar carpeta",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (!string.IsNullOrWhiteSpace(rutaSugerida) && Directory.Exists(rutaSugerida))
        {
            dialogo.SelectedPath = rutaSugerida;
        }

        return dialogo.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? dialogo.SelectedPath
            : null;
    }
}
