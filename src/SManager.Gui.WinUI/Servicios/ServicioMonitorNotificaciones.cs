using SManager.Gui.WinUI.Models;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Estado mínimo de un par para detectar cambios y rutas inaccesibles.</summary>
public sealed record SnapshotParNotificacion(
    string IdPar,
    string Nombre,
    string RutaOrigen,
    string RutaDestino,
    bool Habilitado,
    bool Pausado);

/// <summary>Datos que la GUI aporta en cada ciclo de refresco (~500 ms).</summary>
public sealed class ContextoEvaluacionNotificaciones
{
    public required bool NotificacionesHabilitadas { get; init; }

    public required string Perfil { get; init; }

    public required bool EnEjecucion { get; init; }

    public required int CopiadosSesion { get; init; }

    public required int ErroresSesion { get; init; }

    public required IReadOnlyList<SnapshotParNotificacion> Pares { get; init; }

    public required IReadOnlyList<LineaRegistroViewModel> LineasRegistro { get; init; }
}

/// <summary>Detecta transiciones de estado, rutas, log y pares; emite toasts sin spam.</summary>
public sealed class ServicioMonitorNotificaciones
{
    private bool? _ultimoEnEjecucion;
    private int _ultimosErrores;
    private bool _detencionIntencional;
    private int _lineasLogAnalizadas;
    private bool _notificadoDiscoLlenoSesion;
    private bool _notificadoPermisosSesion;

    private readonly HashSet<string> _rutasInaccesiblesNotificadas = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (bool Habilitado, bool Pausado)> _snapshotPares = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Marca la próxima detención como solicitada por el usuario (Detener / bandeja).</summary>
    public void MarcarDetencionIntencional() => _detencionIntencional = true;

    public void Reiniciar()
    {
        _ultimoEnEjecucion = null;
        _ultimosErrores = 0;
        _detencionIntencional = false;
        _lineasLogAnalizadas = 0;
        _notificadoDiscoLlenoSesion = false;
        _notificadoPermisosSesion = false;
        _rutasInaccesiblesNotificadas.Clear();
        _snapshotPares.Clear();
    }

    /// <summary>Evalúa el estado IPC, rutas, log y pares; muestra toasts cuando cambia la situación.</summary>
    public void Evaluar(ContextoEvaluacionNotificaciones contexto)
    {
        if (!contexto.NotificacionesHabilitadas)
        {
            ActualizarSnapshotInterno(contexto);
            return;
        }

        if (_ultimoEnEjecucion is null)
        {
            ActualizarSnapshotInterno(contexto);
            return;
        }

        EvaluarInicioSincronizacion(contexto);
        EvaluarFinSincronizacion(contexto);
        EvaluarErroresAcumulados(contexto);
        EvaluarDisponibilidadRutas(contexto);
        EvaluarErroresLog(contexto);
        EvaluarCambiosPares(contexto);

        ActualizarSnapshotInterno(contexto);
    }

    private void EvaluarInicioSincronizacion(ContextoEvaluacionNotificaciones contexto)
    {
        if (contexto.EnEjecucion && _ultimoEnEjecucion == false)
        {
            ServicioNotificacionesWindows.Mostrar(
                "Sincronización iniciada",
                $"Perfil «{contexto.Perfil}»: el demonio está copiando archivos.",
                new OpcionesNotificacionToast { SeccionDestino = "monitor" });
        }
    }

    private void EvaluarFinSincronizacion(ContextoEvaluacionNotificaciones contexto)
    {
        if (!contexto.EnEjecucion && _ultimoEnEjecucion == true)
        {
            if (_detencionIntencional)
            {
                if (contexto.ErroresSesion > 0)
                {
                    ServicioNotificacionesWindows.Mostrar(
                        "Sincronización detenida con errores",
                        ResumenSesion(contexto),
                        new OpcionesNotificacionToast { SeccionDestino = "registro" });
                }
                else
                {
                    ServicioNotificacionesWindows.Mostrar(
                        "Sincronización completada correctamente",
                        ResumenSesion(contexto),
                        new OpcionesNotificacionToast { SeccionDestino = "inicio" });
                }
            }
            else
            {
                ServicioNotificacionesWindows.Mostrar(
                    "Demonio detenido inesperadamente",
                    $"Perfil «{contexto.Perfil}»: el proceso de sincronización dejó de ejecutarse sin un comando de detener.{Environment.NewLine}{ResumenSesion(contexto)}",
                    new OpcionesNotificacionToast { SeccionDestino = "monitor" });
            }

            _detencionIntencional = false;
        }
    }

    private void EvaluarErroresAcumulados(ContextoEvaluacionNotificaciones contexto)
    {
        if (contexto.EnEjecucion
            && contexto.ErroresSesion > _ultimosErrores
            && contexto.ErroresSesion - _ultimosErrores >= 5)
        {
            ServicioNotificacionesWindows.Mostrar(
                "Errores durante la sincronización",
                $"Perfil «{contexto.Perfil}»: {contexto.ErroresSesion:N0} errores acumulados. Revisa el Registro.",
                new OpcionesNotificacionToast { SeccionDestino = "registro" });
        }
    }

    private void EvaluarDisponibilidadRutas(ContextoEvaluacionNotificaciones contexto)
    {
        if (!contexto.EnEjecucion)
        {
            _rutasInaccesiblesNotificadas.Clear();
            return;
        }

        foreach (var par in contexto.Pares.Where(p => p.Habilitado && !p.Pausado))
        {
            EvaluarRutaPar(par, par.RutaDestino, esDestino: true);
            EvaluarRutaPar(par, par.RutaOrigen, esDestino: false);
        }
    }

    private void EvaluarRutaPar(
        SnapshotParNotificacion par,
        string ruta,
        bool esDestino)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            return;
        }

        var clave = $"{par.IdPar}|{(esDestino ? "destino" : "origen")}";
        var accesible = RutaAccesible(ruta);

        if (!accesible)
        {
            if (!_rutasInaccesiblesNotificadas.Add(clave))
            {
                return;
            }

            var titulo = esDestino ? "Destino no disponible" : "Origen no disponible";
            var mensaje = esDestino
                ? $"Par «{par.Nombre}»: no se puede acceder al destino.{Environment.NewLine}{ruta}"
                : $"Par «{par.Nombre}»: no se puede acceder al origen.{Environment.NewLine}{ruta}";

            if (EsRutaRed(ruta))
            {
                mensaje += $"{Environment.NewLine}Comprueba que la unidad de red esté conectada.";
            }

            ServicioNotificacionesWindows.Mostrar(
                titulo,
                mensaje,
                new OpcionesNotificacionToast { SeccionDestino = "pares" });
        }
        else
        {
            _rutasInaccesiblesNotificadas.Remove(clave);
        }
    }

    private void EvaluarErroresLog(ContextoEvaluacionNotificaciones contexto)
    {
        if (!contexto.EnEjecucion)
        {
            return;
        }

        var lineas = contexto.LineasRegistro;
        if (lineas.Count <= _lineasLogAnalizadas)
        {
            return;
        }

        for (var indice = _lineasLogAnalizadas; indice < lineas.Count; indice++)
        {
            var linea = lineas[indice];
            if (!string.Equals(linea.Nivel, "ERROR", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var texto = $"{linea.Mensaje} {linea.TextoCompleto}";

            if (!_notificadoDiscoLlenoSesion && EsErrorDiscoLleno(texto))
            {
                _notificadoDiscoLlenoSesion = true;
                ServicioNotificacionesWindows.Mostrar(
                    "Disco lleno",
                    $"Perfil «{contexto.Perfil}»: no hay espacio suficiente para seguir copiando archivos. Libera espacio en el destino.",
                    new OpcionesNotificacionToast { SeccionDestino = "registro" });
            }

            if (!_notificadoPermisosSesion && EsErrorPermisosInsuficientes(texto))
            {
                _notificadoPermisosSesion = true;
                ServicioNotificacionesWindows.Mostrar(
                    "Permisos insuficientes",
                    $"Perfil «{contexto.Perfil}»: SManager no puede leer o escribir en alguna ruta. Revisa permisos o ejecuta como administrador.",
                    new OpcionesNotificacionToast { SeccionDestino = "registro" });
            }
        }

        _lineasLogAnalizadas = lineas.Count;
    }

    private void EvaluarCambiosPares(ContextoEvaluacionNotificaciones contexto)
    {
        if (!contexto.EnEjecucion)
        {
            SincronizarSnapshotPares(contexto.Pares);
            return;
        }

        foreach (var par in contexto.Pares)
        {
            if (!_snapshotPares.TryGetValue(par.IdPar, out var anterior))
            {
                _snapshotPares[par.IdPar] = (par.Habilitado, par.Pausado);
                continue;
            }

            if (anterior.Habilitado && !par.Habilitado)
            {
                ServicioNotificacionesWindows.Mostrar(
                    "Par desactivado",
                    $"Par «{par.Nombre}» desactivado mientras la sincronización sigue en ejecución.",
                    new OpcionesNotificacionToast { SeccionDestino = "pares" });
            }
            else if (anterior.Habilitado && par.Habilitado && !anterior.Pausado && par.Pausado)
            {
                ServicioNotificacionesWindows.Mostrar(
                    "Par pausado",
                    $"Par «{par.Nombre}» pausado. No copiará archivos hasta que lo reanudes.",
                    new OpcionesNotificacionToast { SeccionDestino = "pares" });
            }

            _snapshotPares[par.IdPar] = (par.Habilitado, par.Pausado);
        }

        var idsActuales = contexto.Pares.Select(p => p.IdPar).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var idObsoleto in _snapshotPares.Keys.Where(id => !idsActuales.Contains(id)).ToList())
        {
            _snapshotPares.Remove(idObsoleto);
        }

        var paresActivos = contexto.Pares.Count(p => p.Habilitado && !p.Pausado);
        if (contexto.Pares.Count > 0 && paresActivos == 0)
        {
            var claveTodosInactivos = "__todos_inactivos__";
            if (_rutasInaccesiblesNotificadas.Add(claveTodosInactivos))
            {
                ServicioNotificacionesWindows.Mostrar(
                    "Sin pares activos",
                    $"Perfil «{contexto.Perfil}»: todos los pares están pausados o desactivados.",
                    new OpcionesNotificacionToast { SeccionDestino = "pares" });
            }
        }
        else
        {
            _rutasInaccesiblesNotificadas.Remove("__todos_inactivos__");
        }
    }

    private void ActualizarSnapshotInterno(ContextoEvaluacionNotificaciones contexto)
    {
        _ultimoEnEjecucion = contexto.EnEjecucion;
        _ultimosErrores = contexto.ErroresSesion;

        if (!contexto.EnEjecucion)
        {
            _lineasLogAnalizadas = contexto.LineasRegistro.Count;
        }
    }

    private void SincronizarSnapshotPares(IReadOnlyList<SnapshotParNotificacion> pares)
    {
        _snapshotPares.Clear();
        foreach (var par in pares)
        {
            _snapshotPares[par.IdPar] = (par.Habilitado, par.Pausado);
        }
    }

    private static string ResumenSesion(ContextoEvaluacionNotificaciones contexto) =>
        $"Perfil «{contexto.Perfil}»: {contexto.CopiadosSesion:N0} archivos copiados, {contexto.ErroresSesion:N0} errores.";

    private static bool RutaAccesible(string ruta)
    {
        try
        {
            return Directory.Exists(ruta);
        }
        catch
        {
            return false;
        }
    }

    private static bool EsRutaRed(string ruta) =>
        ruta.StartsWith(@"\\", StringComparison.Ordinal);

    private static bool EsErrorDiscoLleno(string texto) =>
        Contiene(texto,
            "not enough space",
            "no space left",
            "disk full",
            "disco lleno",
            "espacio en disco",
            "espacio insuficiente",
            "0x80070070",
            "ERROR_DISK_FULL");

    private static bool EsErrorPermisosInsuficientes(string texto) =>
        Contiene(texto,
            "UnauthorizedAccessException",
            "Access to the path is denied",
            "permisos",
            "acceso denegado",
            "Sin permisos");

    private static bool Contiene(string texto, params string[] fragmentos)
    {
        foreach (var fragmento in fragmentos)
        {
            if (texto.Contains(fragmento, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
