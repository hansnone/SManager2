using Microsoft.UI.Xaml;

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
