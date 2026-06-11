using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.Storage.Pickers;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Captura un elemento visual WinUI y lo guarda como PNG o JPG.</summary>
public static class ServicioExportarImagen
{
    /// <summary>Pide ruta al usuario, captura <paramref name="elemento"/> sin mostrarlo en pantalla y guarda.</summary>
    public static async Task<bool> ExportarAsync(FrameworkElement elemento, string nombreSugerido)
    {
        var ruta = await ElegirRutaGuardadoAsync(nombreSugerido);
        if (string.IsNullOrWhiteSpace(ruta))
        {
            return false;
        }

        await GuardarCapturaEnRutaAsync(elemento, ruta);
        return true;
    }

    /// <summary>Diálogo «Guardar como» con la API moderna de WinAppSDK (unpackaged).</summary>
    private static async Task<string?> ElegirRutaGuardadoAsync(string nombreSugerido)
    {
        var hwnd = WindowNative.GetWindowHandle(App.Window);
        var idVentana = Win32Interop.GetWindowIdFromWindow(hwnd);

        var selector = new FileSavePicker(idVentana)
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = nombreSugerido,
            Title = "Guardar captura de pares"
        };
        selector.FileTypeChoices.Add("PNG", [".png"]);
        selector.FileTypeChoices.Add("JPG", [".jpg", ".jpeg"]);

        var archivo = await selector.PickSaveFileAsync();
        return archivo?.Path;
    }

    /// <summary>Renderiza el elemento fuera de vista (opacidad 0) para no interrumpir al usuario.</summary>
    private static async Task GuardarCapturaEnRutaAsync(FrameworkElement elemento, string rutaArchivo)
    {
        var visibilidadOriginal = elemento.Visibility;
        var opacidadOriginal = elemento.Opacity;

        try
        {
            elemento.Visibility = Visibility.Visible;
            elemento.Opacity = 0;
            elemento.IsHitTestVisible = false;
            elemento.UpdateLayout();

            var render = new RenderTargetBitmap();
            await render.RenderAsync(elemento);

            var buffer = (await render.GetPixelsAsync()).ToArray();
            var extension = Path.GetExtension(rutaArchivo);
            var esJpg = extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);

            using var flujo = File.OpenWrite(rutaArchivo);
            using IRandomAccessStream flujoWinRt = flujo.AsRandomAccessStream();

            var encoderId = esJpg ? BitmapEncoder.JpegEncoderId : BitmapEncoder.PngEncoderId;
            var encoder = await BitmapEncoder.CreateAsync(encoderId, flujoWinRt);

            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                esJpg ? BitmapAlphaMode.Ignore : BitmapAlphaMode.Premultiplied,
                (uint)render.PixelWidth,
                (uint)render.PixelHeight,
                96,
                96,
                buffer);

            await encoder.FlushAsync();
        }
        finally
        {
            elemento.Opacity = opacidadOriginal;
            elemento.IsHitTestVisible = true;
            elemento.Visibility = visibilidadOriginal;
        }
    }
}
