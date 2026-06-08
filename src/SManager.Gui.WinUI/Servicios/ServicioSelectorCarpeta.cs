using Microsoft.UI;
using Microsoft.Windows.Storage.Pickers;
using WinRT.Interop;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Selector de carpetas nativo de Windows enlazado a la ventana WinUI.</summary>
public static class ServicioSelectorCarpeta
{
    /// <summary>Abre el diálogo de carpetas. Debe invocarse desde el hilo UI.</summary>
    public static async Task<string?> ElegirCarpetaAsync(string? rutaSugerida = null)
    {
        if (!App.DispatcherQueue.HasThreadAccess)
        {
            var completado = new TaskCompletionSource<string?>();
            App.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    completado.SetResult(await ElegirCarpetaEnHiloUiAsync(rutaSugerida));
                }
                catch (Exception ex)
                {
                    completado.SetException(ex);
                }
            });
            return await completado.Task;
        }

        return await ElegirCarpetaEnHiloUiAsync(rutaSugerida);
    }

    private static async Task<string?> ElegirCarpetaEnHiloUiAsync(string? rutaSugerida)
    {
        var hwnd = WindowNative.GetWindowHandle(App.Window);
        var idVentana = Win32Interop.GetWindowIdFromWindow(hwnd);

        // API de Windows App SDK pensada para WinUI 3 desktop (no requiere FileTypeFilter).
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
}
