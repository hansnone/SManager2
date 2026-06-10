using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>
/// Garantiza una sola GUI por usuario de Windows (evita colisiones en JSON e IPC).
/// Si el usuario abre un segundo acceso directo, restaura la ventana ya abierta.
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

    /// <summary>
    /// Restaura y enfoca la instancia ya en ejecución (doble clic en el acceso directo).
    /// </summary>
    private static void TraerVentanaExistenteAlFrente()
    {
        try
        {
            // Evento nombrado: funciona aunque la ventana principal esté oculta (AppWindow.Hide).
            ServicioSenalRestaurarVentana.SolicitarRestaurar();

            if (ActivarInstanciaPorProceso())
            {
                return;
            }

            var handleVentana = BuscarVentanaPorTitulo(TituloVentanaPrincipal);
            if (handleVentana != IntPtr.Zero)
            {
                RestaurarYEnfocar(handleVentana);
            }
        }
        catch
        {
            // Si no podemos enfocar, la segunda instancia simplemente se cierra.
        }
    }

    /// <summary>
    /// Localiza otro proceso del mismo ejecutable en la misma sesión y trae su ventana al frente.
    /// </summary>
    private static bool ActivarInstanciaPorProceso()
    {
        using var procesoActual = Process.GetCurrentProcess();
        var nombreProceso = procesoActual.ProcessName;
        foreach (var proceso in Process.GetProcessesByName(nombreProceso))
        {
            try
            {
                if (proceso.Id == procesoActual.Id)
                {
                    continue;
                }

                if (proceso.SessionId != procesoActual.SessionId)
                {
                    continue;
                }

                if (!EsMismoEjecutable(procesoActual, proceso))
                {
                    continue;
                }

                var handleVentana = proceso.MainWindowHandle;
                if (handleVentana == IntPtr.Zero)
                {
                    handleVentana = BuscarVentanaDelProceso(proceso.Id, TituloVentanaPrincipal);
                }

                if (handleVentana == IntPtr.Zero)
                {
                    continue;
                }

                RestaurarYEnfocar(handleVentana);
                return true;
            }
            finally
            {
                proceso.Dispose();
            }
        }

        return false;
    }

    /// <summary>Evita confundir otro .exe con el mismo nombre de proceso en disco.</summary>
    private static bool EsMismoEjecutable(Process procesoActual, Process otroProceso)
    {
        try
        {
            var rutaActual = procesoActual.MainModule?.FileName;
            var rutaOtro = otroProceso.MainModule?.FileName;
            if (string.IsNullOrEmpty(rutaActual) || string.IsNullOrEmpty(rutaOtro))
            {
                return true;
            }

            return string.Equals(rutaActual, rutaOtro, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Sin permiso para leer MainModule: confiar en nombre + sesión.
            return true;
        }
    }

    private static void RestaurarYEnfocar(IntPtr handleVentana)
    {
        MostrarVentana(handleVentana, ComandoRestaurarVentana);
        EstablecerVentanaEnPrimerPlano(handleVentana);
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

    private static IntPtr BuscarVentanaDelProceso(int idProceso, string tituloEsperado)
    {
        IntPtr encontrado = IntPtr.Zero;
        EnumWindows((handle, _) =>
        {
            ObtenerIdProcesoVentana(handle, out var idVentana);
            if (idVentana != idProceso)
            {
                return true;
            }

            var longitud = ObtenerLongitudTituloVentana(handle);
            if (longitud <= 0)
            {
                return true;
            }

            var buffer = new StringBuilder(longitud + 1);
            _ = ObtenerTituloVentana(handle, buffer, buffer.Capacity);
            if (buffer.ToString().Contains(tituloEsperado, StringComparison.OrdinalIgnoreCase))
            {
                encontrado = handle;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return encontrado;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    private static void ObtenerIdProcesoVentana(IntPtr handle, out uint idProceso) =>
        GetWindowThreadProcessId(handle, out idProceso);

    private static int ObtenerLongitudTituloVentana(IntPtr handle) =>
        GetWindowTextLength(handle);

    private static int ObtenerTituloVentana(IntPtr handle, StringBuilder buffer, int maximo) =>
        GetWindowText(handle, buffer, maximo);
}
