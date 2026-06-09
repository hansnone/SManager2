using System.Runtime.InteropServices;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>
/// Garantiza una sola GUI por usuario de Windows (evita colisiones en JSON e IPC).
/// </summary>
public static class ServicioInstanciaUnica
{
    private const string PrefijoMutex = "SManager2_GUI_Mutex_";
    private const string TituloVentanaPrincipal = "SManager 2.0";
    private const int ComandoRestaurarVentana = 9;

    private static Mutex? _mutexInstancia;

    /// <summary>
    /// Intenta adquirir el mutex de instancia única.
    /// Si ya hay otra GUI, trae su ventana al frente y devuelve false.
    /// </summary>
    public static bool IntentarAdquirirInstanciaUnica()
    {
        var nombreMutex = PrefijoMutex + Environment.UserName;
        _mutexInstancia = new Mutex(initiallyOwned: true, nombreMutex, out var esInstanciaNueva);

        if (esInstanciaNueva)
        {
            return true;
        }

        TraerVentanaExistenteAlFrente();
        return false;
    }

    /// <summary>Restaura y enfoca la ventana ya abierta (doble clic en el acceso directo).</summary>
    private static void TraerVentanaExistenteAlFrente()
    {
        try
        {
            var handleVentana = BuscarVentanaPorTitulo(TituloVentanaPrincipal);
            if (handleVentana == IntPtr.Zero)
            {
                return;
            }

            MostrarVentana(handleVentana, ComandoRestaurarVentana);
            EstablecerVentanaEnPrimerPlano(handleVentana);
        }
        catch
        {
            // Si no podemos enfocar, la segunda instancia simplemente se cierra.
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr BuscarVentanaPorTituloNativo(string? claseVentana, string tituloVentana);

    private static IntPtr BuscarVentanaPorTitulo(string titulo) =>
        BuscarVentanaPorTituloNativo(null, titulo);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handleVentana);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr handleVentana, int comandoMostrar);

    private static void EstablecerVentanaEnPrimerPlano(IntPtr handleVentana) =>
        SetForegroundWindow(handleVentana);

    private static void MostrarVentana(IntPtr handleVentana, int comando) =>
        ShowWindow(handleVentana, comando);
}
