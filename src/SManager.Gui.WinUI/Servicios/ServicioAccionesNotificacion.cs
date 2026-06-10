namespace SManager.Gui.WinUI.Servicios;

/// <summary>Canal de eventos entre los toasts de Windows y la ventana principal.</summary>
public static class ServicioAccionesNotificacion
{
    private static AccionPendienteNotificacion? _accionPendiente;

    public static event Action? AbrirVentanaSolicitado;

    /// <summary>Sección de navegación: inicio, pares, monitor, registro, estadisticas, guia, ajustes.</summary>
    public static event Action<string>? VerDetallesSolicitado;

    public static void ProcesarActivacion(IDictionary<string, string> argumentos)
    {
        if (argumentos.Count == 0)
        {
            EncolarOInvocarAbrir();
            return;
        }

        var accion = argumentos.TryGetValue("accion", out var valorAccion)
            ? valorAccion
            : "abrir";

        switch (accion.ToLowerInvariant())
        {
            case "ver_detalles":
                var seccion = argumentos.TryGetValue("seccion", out var valorSeccion)
                    ? valorSeccion
                    : "registro";
                EncolarOInvocarVerDetalles(seccion);
                break;

            case "abrir":
            default:
                EncolarOInvocarAbrir();
                break;
        }
    }

    /// <summary>Reproduce una activación retenida hasta que MainPage se suscriba.</summary>
    public static void ReproducirAccionPendiente()
    {
        var pendiente = _accionPendiente;
        if (pendiente is null)
        {
            return;
        }

        _accionPendiente = null;

        if (pendiente.EsVerDetalles)
        {
            VerDetallesSolicitado?.Invoke(pendiente.Seccion ?? "registro");
        }
        else
        {
            AbrirVentanaSolicitado?.Invoke();
        }
    }

    private static void EncolarOInvocarAbrir()
    {
        if (AbrirVentanaSolicitado is null)
        {
            _accionPendiente = AccionPendienteNotificacion.Abrir();
            return;
        }

        AbrirVentanaSolicitado.Invoke();
    }

    private static void EncolarOInvocarVerDetalles(string seccion)
    {
        if (VerDetallesSolicitado is null)
        {
            _accionPendiente = AccionPendienteNotificacion.VerDetalles(seccion);
            return;
        }

        VerDetallesSolicitado.Invoke(seccion);
    }

    private sealed record AccionPendienteNotificacion(bool EsVerDetalles, string? Seccion)
    {
        public static AccionPendienteNotificacion Abrir() => new(EsVerDetalles: false, Seccion: null);

        public static AccionPendienteNotificacion VerDetalles(string seccion) =>
            new(EsVerDetalles: true, Seccion: seccion);
    }
}
