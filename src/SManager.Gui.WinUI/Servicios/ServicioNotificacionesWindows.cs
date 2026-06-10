using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Opciones para toasts con botones de acción en la GUI.</summary>
public sealed class OpcionesNotificacionToast
{
    /// <summary>Sección a abrir con «Ver detalles» (monitor, registro, pares, inicio…).</summary>
    public string? SeccionDestino { get; init; }

    /// <summary>Si false, solo muestra «Ver detalles» cuando hay sección.</summary>
    public bool IncluirAbrirApp { get; init; } = true;
}

/// <summary>Notificaciones toast de Windows para eventos de sincronización.</summary>
public static class ServicioNotificacionesWindows
{
    private const string IdAplicacion = "SManager.Gui.WinUI";

    private static bool _registrado;
    private static bool _handlerRegistrado;

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

            if (!_handlerRegistrado)
            {
                AppNotificationManager.Default.NotificationInvoked += GestionarActivacionNotificacion;
                _handlerRegistrado = true;
            }
        }
        catch
        {
            // Entornos sin soporte toast: la app sigue funcionando sin notificaciones.
        }
    }

    /// <summary>Procesa la activación cuando la app arranca desde un toast (app cerrada).</summary>
    public static void ProcesarActivacionEnArranque()
    {
        if (!_registrado)
        {
            return;
        }

        try
        {
            var activacion = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activacion?.Kind != ExtendedActivationKind.AppNotification)
            {
                return;
            }

            if (activacion.Data is AppNotificationActivatedEventArgs argumentosNotificacion
                && argumentosNotificacion.Arguments.Count > 0)
            {
                ServicioAccionesNotificacion.ProcesarActivacion(argumentosNotificacion.Arguments);
            }
        }
        catch
        {
            // Sin activación por toast en este arranque.
        }
    }

    public static void Mostrar(string titulo, string mensaje) =>
        Mostrar(titulo, mensaje, opciones: null);

    public static void Mostrar(string titulo, string mensaje, OpcionesNotificacionToast? opciones)
    {
        if (!_registrado)
        {
            return;
        }

        try
        {
            var constructor = new AppNotificationBuilder()
                .AddText(titulo)
                .AddText(mensaje);

            if (opciones is not null)
            {
                AgregarAcciones(constructor, opciones);
            }

            AppNotificationManager.Default.Show(constructor.BuildNotification());
        }
        catch
        {
            // No interrumpir la GUI si el sistema rechaza la notificación.
        }
    }

    private static void AgregarAcciones(AppNotificationBuilder constructor, OpcionesNotificacionToast opciones)
    {
        if (!string.IsNullOrWhiteSpace(opciones.SeccionDestino))
        {
            constructor
                .AddArgument("accion", "ver_detalles")
                .AddArgument("seccion", opciones.SeccionDestino);

            constructor.AddButton(
                new AppNotificationButton("Ver detalles")
                    .AddArgument("accion", "ver_detalles")
                    .AddArgument("seccion", opciones.SeccionDestino));
        }

        if (opciones.IncluirAbrirApp)
        {
            constructor.AddButton(
                new AppNotificationButton("Abrir SManager")
                    .AddArgument("accion", "abrir"));
        }
    }

    private static void GestionarActivacionNotificacion(
        AppNotificationManager remitente,
        AppNotificationActivatedEventArgs argumentos)
    {
        ServicioAccionesNotificacion.ProcesarActivacion(argumentos.Arguments);
    }
}
