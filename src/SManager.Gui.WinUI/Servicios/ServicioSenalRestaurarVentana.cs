namespace SManager.Gui.WinUI.Servicios;

/// <summary>
/// Señal entre procesos para restaurar la GUI cuando el usuario abre un segundo acceso directo
/// y la ventana principal está oculta en la bandeja.
/// </summary>
public static class ServicioSenalRestaurarVentana
{
    private static readonly string NombreEvento = "SManager2_RestaurarGUI_" + Environment.UserName;

    private static CancellationTokenSource? _cancelacionEscucha;
    private static EventWaitHandle? _eventoRestaurar;

    /// <summary>Crea el evento y escucha peticiones de restauración en segundo plano.</summary>
    public static void IniciarEscucha(Action restaurarVentana)
    {
        DetenerEscucha();

        _eventoRestaurar = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            NombreEvento,
            out _);

        _cancelacionEscucha = new CancellationTokenSource();
        var token = _cancelacionEscucha.Token;

        Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!_eventoRestaurar.WaitOne(millisecondsTimeout: 500))
                    {
                        continue;
                    }

                    App.DispatcherQueue.TryEnqueue(() => restaurarVentana());
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }, token);
    }

    /// <summary>Notifica a la instancia en ejecución que debe mostrarse.</summary>
    public static void SolicitarRestaurar()
    {
        try
        {
            using var evento = EventWaitHandle.OpenExisting(NombreEvento);
            evento.Set();
        }
        catch
        {
            // Sin instancia escuchando: el llamador usará otros métodos de respaldo.
        }
    }

    public static void DetenerEscucha()
    {
        _cancelacionEscucha?.Cancel();
        _cancelacionEscucha?.Dispose();
        _cancelacionEscucha = null;

        _eventoRestaurar?.Dispose();
        _eventoRestaurar = null;
    }
}
