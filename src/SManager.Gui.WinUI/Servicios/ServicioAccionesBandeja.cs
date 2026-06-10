namespace SManager.Gui.WinUI.Servicios;

/// <summary>Canal de eventos entre la bandeja del sistema y la ventana principal.</summary>
public static class ServicioAccionesBandeja
{
    public static event Action? AbrirVentanaSolicitado;

    public static event Action? IniciarSincronizacionSolicitado;

    public static event Action? DetenerSincronizacionSolicitado;

    public static event Action? VerMonitorSolicitado;

    public static event Action? SalirAplicacionSolicitado;

    public static void SolicitarAbrirVentana() => AbrirVentanaSolicitado?.Invoke();

    public static void SolicitarIniciar() => IniciarSincronizacionSolicitado?.Invoke();

    public static void SolicitarDetener() => DetenerSincronizacionSolicitado?.Invoke();

    public static void SolicitarVerMonitor() => VerMonitorSolicitado?.Invoke();

    public static void SolicitarSalir() => SalirAplicacionSolicitado?.Invoke();
}
