using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Notificaciones toast de Windows para eventos de sincronización.</summary>
public static class ServicioNotificacionesWindows
{
    private const string IdAplicacion = "SManager.Gui.WinUI";

    private static bool _registrado;

    public static void Inicializar()
    {
        if (_registrado)
        {
            return;
        }

        try
        {
            var rutaIcono = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            var uriIcono = File.Exists(rutaIcono)
                ? new Uri(rutaIcono)
                : new Uri("ms-appx:///Assets/AppIcon.ico");
            AppNotificationManager.Default.Register(IdAplicacion, uriIcono);
            _registrado = true;
        }
        catch
        {
            // Entornos sin soporte toast: la app sigue funcionando sin notificaciones.
        }
    }

    public static void Mostrar(string titulo, string mensaje)
    {
        if (!_registrado)
        {
            return;
        }

        try
        {
            var notificacion = new AppNotificationBuilder()
                .AddText(titulo)
                .AddText(mensaje)
                .BuildNotification();

            AppNotificationManager.Default.Show(notificacion);
        }
        catch
        {
            // No interrumpir la GUI si el sistema rechaza la notificación.
        }
    }
}

/// <summary>Detecta transiciones de estado y emite notificaciones sin repetir spam.</summary>
public sealed class ServicioMonitorNotificaciones
{
    private bool? _ultimoEnEjecucion;
    private int _ultimosErrores;
    private int _ultimosCopiados;

    public void Reiniciar()
    {
        _ultimoEnEjecucion = null;
        _ultimosErrores = 0;
        _ultimosCopiados = 0;
    }

    /// <summary>Evalúa el estado IPC y muestra toasts cuando cambia la situación relevante.</summary>
    public void Evaluar(
        bool notificacionesHabilitadas,
        string perfil,
        bool enEjecucion,
        int copiadosSesion,
        int erroresSesion)
    {
        if (!notificacionesHabilitadas)
        {
            _ultimoEnEjecucion = enEjecucion;
            _ultimosCopiados = copiadosSesion;
            _ultimosErrores = erroresSesion;
            return;
        }

        if (_ultimoEnEjecucion is null)
        {
            _ultimoEnEjecucion = enEjecucion;
            _ultimosCopiados = copiadosSesion;
            _ultimosErrores = erroresSesion;
            return;
        }

        if (enEjecucion && _ultimoEnEjecucion == false)
        {
            ServicioNotificacionesWindows.Mostrar(
                "Sincronización iniciada",
                $"Perfil «{perfil}»: el demonio está copiando archivos.");
        }

        if (!enEjecucion && _ultimoEnEjecucion == true)
        {
            if (erroresSesion > 0)
            {
                ServicioNotificacionesWindows.Mostrar(
                    "Sincronización detenida con errores",
                    $"Perfil «{perfil}»: {copiadosSesion:N0} copiados, {erroresSesion:N0} errores.");
            }
            else
            {
                ServicioNotificacionesWindows.Mostrar(
                    "Sincronización detenida",
                    $"Perfil «{perfil}»: {copiadosSesion:N0} archivos copiados en la sesión.");
            }
        }
        else if (enEjecucion && erroresSesion > _ultimosErrores && erroresSesion - _ultimosErrores >= 5)
        {
            ServicioNotificacionesWindows.Mostrar(
                "Errores durante la sincronización",
                $"Perfil «{perfil}»: {erroresSesion:N0} errores acumulados. Revisa el Registro.");
        }

        _ultimoEnEjecucion = enEjecucion;
        _ultimosCopiados = copiadosSesion;
        _ultimosErrores = erroresSesion;
    }
}
