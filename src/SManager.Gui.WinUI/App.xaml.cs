using Microsoft.UI.Xaml;
using SManager.Ipc;

namespace SManager.Gui.WinUI;

public partial class App : Application
{
    public static Window Window { get; private set; } = null!;

    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    public App()
    {
        InitializeComponent();
        UnhandledException += App_UnhandledException;
    }

    /// <summary>Registra fallos no capturados para diagnosticar cierres inesperados.</summary>
    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(RutasDatos.ResolverRaiz());
            var ruta = Path.Combine(RutasDatos.ResolverRaiz(), "gui_crash.log");
            var linea =
                $"[{DateTimeOffset.Now:O}] {e.Exception.GetType().Name}: {e.Exception.Message}{Environment.NewLine}"
                + e.Exception.StackTrace
                + Environment.NewLine
                + new string('-', 60)
                + Environment.NewLine;
            File.AppendAllText(ruta, linea);
        }
        catch
        {
            // Si no podemos escribir el log, al menos no añadimos otro fallo.
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Debe existir antes de crear MainWindow: MainPage instancia el ViewModel en su ctor.
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        Window = new MainWindow
        {
            Title = "SManager 2.0"
        };
        Window.AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 840));
        Window.Activate();
    }
}
