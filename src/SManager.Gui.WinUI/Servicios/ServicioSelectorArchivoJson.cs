using Microsoft.UI;
using Microsoft.Windows.Storage.Pickers;
using WinRT.Interop;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Selector de archivos JSON para la configuración del perfil.</summary>
public static class ServicioSelectorArchivoJson
{
    /// <summary>Abre el diálogo para elegir un configuracion.json existente.</summary>
    public static Task<string?> ElegirArchivoExistenteAsync(string? rutaSugerida = null) =>
        EjecutarEnHiloUiAsync(abrir: true, rutaSugerida);

    /// <summary>Abre el diálogo para indicar dónde guardar un JSON nuevo.</summary>
    public static Task<string?> ElegirRutaGuardadoAsync(string? rutaSugerida = null) =>
        EjecutarEnHiloUiAsync(abrir: false, rutaSugerida);

    private static async Task<string?> EjecutarEnHiloUiAsync(bool abrir, string? rutaSugerida)
    {
        if (!App.DispatcherQueue.HasThreadAccess)
        {
            var completado = new TaskCompletionSource<string?>();
            App.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    completado.SetResult(await ElegirEnHiloUiAsync(abrir, rutaSugerida));
                }
                catch (Exception ex)
                {
                    completado.SetException(ex);
                }
            });
            return await completado.Task;
        }

        return await ElegirEnHiloUiAsync(abrir, rutaSugerida);
    }

    private static async Task<string?> ElegirEnHiloUiAsync(bool abrir, string? rutaSugerida)
    {
        var hwnd = WindowNative.GetWindowHandle(App.Window);
        var idVentana = Win32Interop.GetWindowIdFromWindow(hwnd);

        if (abrir)
        {
            var selector = new FileOpenPicker(idVentana)
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                ViewMode = PickerViewMode.List,
                Title = "Seleccionar configuracion.json"
            };
            selector.FileTypeFilter.Add(".json");

            if (!string.IsNullOrWhiteSpace(rutaSugerida))
            {
                var carpeta = Path.GetDirectoryName(rutaSugerida);
                if (!string.IsNullOrEmpty(carpeta) && Directory.Exists(carpeta))
                {
                    selector.SuggestedStartFolder = carpeta;
                }
            }

            var archivo = await selector.PickSingleFileAsync();
            return archivo?.Path;
        }

        var guardar = new FileSavePicker(idVentana)
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "configuracion.json",
            Title = "Guardar configuracion.json como"
        };
        guardar.FileTypeChoices.Add("JSON", [".json"]);

        if (!string.IsNullOrWhiteSpace(rutaSugerida))
        {
            var carpeta = Path.GetDirectoryName(rutaSugerida);
            if (!string.IsNullOrEmpty(carpeta) && Directory.Exists(carpeta))
            {
                guardar.SuggestedStartFolder = carpeta;
            }

            guardar.SuggestedFileName = Path.GetFileName(rutaSugerida);
        }

        var destino = await guardar.PickSaveFileAsync();
        return destino?.Path;
    }
}
