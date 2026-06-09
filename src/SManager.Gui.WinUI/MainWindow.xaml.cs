using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace SManager.Gui.WinUI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ConfigurarIconoAplicacion();
        ConfigurarTamanoMinimoVentana();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        RootFrame.Navigate(typeof(MainPage));
    }

    /// <summary>Evita que el panel de acciones y el monitor queden ilegibles en ventanas demasiado estrechas.</summary>
    private void ConfigurarTamanoMinimoVentana()
    {
        if (AppWindow.Presenter is OverlappedPresenter presentador)
        {
            presentador.PreferredMinimumWidth = 880;
            presentador.PreferredMinimumHeight = 560;
        }
    }

    /// <summary>
    /// Icono en taskbar y .exe. TitleBar sin BitmapIconSource: rutas locales fallan en unpackaged.
    /// </summary>
    private void ConfigurarIconoAplicacion()
    {
        try
        {
            var rutaIco = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (!File.Exists(rutaIco))
            {
                return;
            }

            var hwnd = WindowNative.GetWindowHandle(this);
            var idVentana = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow.GetFromWindowId(idVentana).SetIcon(rutaIco);
        }
        catch
        {
            // Icono opcional: no debe impedir abrir la ventana.
        }
    }
}
