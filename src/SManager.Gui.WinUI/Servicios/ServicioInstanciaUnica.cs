using System.Diagnostics;
using System.Runtime.InteropServices;

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
            if (ActivarInstanciaPorProceso())
            {
                return;
            }

            // Respaldo si MainWindowHandle aún no está listo: buscar por título de ventana.
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
}
