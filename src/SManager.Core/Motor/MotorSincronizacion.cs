using Microsoft.Extensions.Logging;
using SManager.Core.Configuracion;
using SManager.Core.Logging;
using SManager.Core.Modelos;
using SManager.Core.Utilidades;
using SManager.Core.Vigia;
using SManager.Core.Workers;
using SManager.Ipc;
using SManager.Ipc.Modelos;

namespace SManager.Core.Motor;

/// <summary>Orquestador del demonio: vigías, pools, IPC, polling y apagado ordenado.</summary>
public sealed class MotorSincronizacion : IAsyncDisposable
{
    private readonly OpcionesDemonio _opciones;
    private readonly ConfiguracionRepositorio _repositorioConfig;
    private readonly ValidadorConfiguracion _validador;
    private readonly ServicioIpc _ipc;
    private readonly ILogger<MotorSincronizacion> _logger;

    private EstadoMotor? _estado;
    private GestorVigias? _gestorVigias;
    private PoolCopiadores? _poolCopiadores;
    private PoolHidratadores? _poolHidratadores;
    private EscritorLog? _escritorLog;
    private CancellationTokenSource? _ctsPrincipal;
    private Task? _tareaBucle;
    private DateTime? _ultimaMarcaConfig;
    private DateTime? _recargaPendienteDesde;
    private readonly Dictionary<string, ResumenParInterno> _resumenPares = new(StringComparer.OrdinalIgnoreCase);
    private readonly MuestreadorRecursosProceso _muestreadorRecursos = new();

    public Task? TareaPrincipal => _tareaBucle;

    public MotorSincronizacion(
        OpcionesDemonio opciones,
        ConfiguracionRepositorio repositorioConfig,
        ValidadorConfiguracion validador,
        ServicioIpc ipc,
        ILogger<MotorSincronizacion> logger)
    {
        _opciones = opciones;
        _repositorioConfig = repositorioConfig;
        _validador = validador;
        _ipc = ipc;
        _logger = logger;
    }

    public async Task IniciarAsync(CancellationToken cancelacionExterna)
    {
        var config = await _repositorioConfig.LeerAsync(_opciones.RutaConfiguracion, cancelacionExterna)
            .ConfigureAwait(false);
        var validacion = _validador.Validar(config);
        if (!validacion.Valida)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, validacion.Errores));
        }

        var rutaActiva = RutasDatos.ObtenerRutaConfiguracionActiva(_opciones.NombrePerfil);
        await CopiaArchivoConReintentos
            .CopiarAsync(_opciones.RutaConfiguracion, rutaActiva, cancelacion: cancelacionExterna)
            .ConfigureAwait(false);

        _estado = new EstadoMotor
        {
            Config = config,
            Pares = config.Pares.ToList(),
            EnEjecucion = true,
            InicioSesionUtc = DateTime.UtcNow
        };

        InicializarTemporizadoresPolling(config);

        InicializarResumenPares();
        _estado.EncolarLog("__ALL__", "INFO", $"SManager 2.0 iniciado (perfil: {_opciones.NombrePerfil}, PID: {Environment.ProcessId})");

        _escritorLog = new EscritorLog(RutasDatos.ObtenerRutaLog(_opciones.NombrePerfil));
        _escritorLog.Iniciar(DrenarLineasLog);

        _gestorVigias = new GestorVigias(_estado);
        _poolCopiadores = new PoolCopiadores(_estado);
        _poolHidratadores = new PoolHidratadores(_estado);

        _gestorVigias.IniciarTodos();
        _poolCopiadores.Iniciar(config.NumCopiadoresParalelos);
        _poolHidratadores.Iniciar(config.NumHidratadoresParalelos);

        _ipc.EscribirPid(_opciones.NombrePerfil, Environment.ProcessId);
        await _ipc.LimpiarComandoAsync(_opciones.NombrePerfil, cancelacionExterna).ConfigureAwait(false);

        try
        {
            _ultimaMarcaConfig = File.GetLastWriteTimeUtc(_opciones.RutaConfiguracion);
        }
        catch
        {
            _ultimaMarcaConfig = null;
        }

        _ctsPrincipal = CancellationTokenSource.CreateLinkedTokenSource(cancelacionExterna);
        _tareaBucle = Task.Run(() => EjecutarBuclePrincipalAsync(_ctsPrincipal.Token), CancellationToken.None);

        _logger.LogInformation("Motor completo iniciado para perfil {Perfil}", _opciones.NombrePerfil);
    }

    public async Task DetenerOrdenadoAsync(CancellationToken cancelacion)
    {
        if (_estado is not null)
        {
            _estado.AceptarNuevosTrabajos = false;
            _estado.EnEjecucion = false;
            _estado.EncolarLog("__ALL__", "INFO", "Apagado ordenado: no se aceptan trabajos nuevos");
        }

        if (_ctsPrincipal is not null)
        {
            await _ctsPrincipal.CancelAsync().ConfigureAwait(false);
        }

        if (_tareaBucle is not null)
        {
            try
            {
                await _tareaBucle.WaitAsync(TimeSpan.FromMinutes(12), cancelacion).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Timeout esperando bucle principal del perfil {Perfil}", _opciones.NombrePerfil);
            }
        }

        if (_gestorVigias is not null)
        {
            await _gestorVigias.DetenerTodosAsync().ConfigureAwait(false);
        }

        if (_poolCopiadores is not null)
        {
            await _poolCopiadores.DetenerAsync(TimeSpan.FromMinutes(2)).ConfigureAwait(false);
        }

        if (_poolHidratadores is not null)
        {
            await _poolHidratadores.DetenerAsync().ConfigureAwait(false);
        }

        if (_estado is not null)
        {
            DrenarLineasLogAlDisco();
            await PublicarEstadoFinalAsync(cancelacion).ConfigureAwait(false);
        }

        if (_escritorLog is not null)
        {
            await _escritorLog.DetenerAsync().ConfigureAwait(false);
        }

        _ipc.EliminarPid(_opciones.NombrePerfil);
        await _ipc.LimpiarComandoAsync(_opciones.NombrePerfil, cancelacion).ConfigureAwait(false);
    }

    private async Task EjecutarBuclePrincipalAsync(CancellationToken cancelacion)
    {
        if (_estado is null)
        {
            return;
        }

        var intervaloMs = Math.Max(200, _estado.Config.IntervaloPublicacionEstadoMs);

        try
        {
            while (!cancelacion.IsCancellationRequested)
            {
                try
                {
                    var continuar = await EjecutarIteracionBucleAsync(cancelacion, intervaloMs).ConfigureAwait(false);
                    if (!continuar)
                    {
                        break;
                    }
                }
                catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error en iteración del bucle principal (perfil {Perfil}, tipo {TipoExcepcion})",
                        _opciones.NombrePerfil,
                        ex.GetType().Name);
                    _estado?.EncolarLog("__ALL__", "ERROR", FormatearErrorBuclePrincipal(ex));

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), cancelacion).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Apagado normal.
        }
        finally
        {
            if (_estado is not null)
            {
                _estado.EnEjecucion = false;
                _estado.AceptarNuevosTrabajos = false;
            }

            await DetenerWorkersSinBucleAsync().ConfigureAwait(false);
            DrenarLineasLogAlDisco();
            await PublicarEstadoFinalAsync(CancellationToken.None).ConfigureAwait(false);
            _ipc.EliminarPid(_opciones.NombrePerfil);
            await _ipc.LimpiarComandoAsync(_opciones.NombrePerfil, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <returns>false si el bucle debe terminar (p. ej. comando APAGAR).</returns>
    private async Task<bool> EjecutarIteracionBucleAsync(CancellationToken cancelacion, int intervaloMs)
    {
        if (_estado is null)
        {
            return true;
        }

        // Prioridad: atender comandos IPC (APAGAR/RECARGAR/PARES/BORRADO) antes de trabajo pesado del ciclo.
        var control = await _ipc.LeerControlPendienteAsync(_opciones.NombrePerfil, cancelacion)
            .ConfigureAwait(false);

        if (control is not null && !string.IsNullOrWhiteSpace(control.Comando))
        {
            var cmd = control.Comando.ToUpperInvariant();
            if (cmd == "APAGAR")
            {
                await _ipc.LimpiarComandoAsync(_opciones.NombrePerfil, cancelacion).ConfigureAwait(false);
                _logger.LogInformation("Señal APAGAR recibida para perfil {Perfil}", _opciones.NombrePerfil);
                SolicitarApagadoInmediato();
                return false;
            }
            else if (cmd == "RECARGAR")
            {
                await _ipc.LimpiarComandoAsync(_opciones.NombrePerfil, cancelacion).ConfigureAwait(false);
                _recargaPendienteDesde = DateTime.UtcNow.AddSeconds(-10);
                await ProcesarRecargaPendienteAsync(cancelacion).ConfigureAwait(false);
            }
            else if (cmd == "DESBLOQUEAR_BORRADO")
            {
                await _ipc.LimpiarComandoAsync(_opciones.NombrePerfil, cancelacion).ConfigureAwait(false);
                var estadoDeseado = control.DesbloquearBorrado ?? true;
                _estado.SesionBorradoDesbloqueada = estadoDeseado;
                _estado.EncolarLog("__ALL__", "INFO", $"[SEGURIDAD] Estado de borrado en origen en la sesión: {(estadoDeseado ? "DESBLOQUEADO (Admin)" : "BLOQUEADO")}");
            }
            else if (cmd == "INICIAR_PARES" && control.IdsPares is { Count: > 0 })
            {
                await _ipc.LimpiarComandoAsync(_opciones.NombrePerfil, cancelacion).ConfigureAwait(false);
                lock (_estado.CandadoPares)
                {
                    foreach (var par in _estado.Pares.Where(p => control.IdsPares.Contains(p.IdPar, StringComparer.OrdinalIgnoreCase)))
                    {
                        par.Habilitado = true;
                        par.Pausado = false;
                        _estado.ProgramarProximoPolling(par);
                        _estado.SolicitarEscaneoPorPolling(par.IdPar);
                        _estado.EncolarLog(par.IdPar, "INFO", $"Par '{par.Nombre}' arrancado/reanudado por demanda.");
                    }
                }
            }
            else if (cmd == "AUTORIZAR_PURGA_ESPEJO" && control.IdsPares is { Count: > 0 })
            {
                await _ipc.LimpiarComandoAsync(_opciones.NombrePerfil, cancelacion).ConfigureAwait(false);
                lock (_estado.CandadoPares)
                {
                    foreach (var par in _estado.Pares.Where(p => control.IdsPares.Contains(p.IdPar, StringComparer.OrdinalIgnoreCase)))
                    {
                        _estado.AutorizacionPurgaMasivaUnaVez[par.IdPar] = true;
                        _estado.PeticionEscaneoCompleto[par.IdPar] = true;
                        _estado.EncolarLog(par.IdPar, "INFO", $"[ESPEJO] Purga masiva autorizada conscientemente para el par '{par.Nombre}'.");
                    }
                }
            }
            else if (cmd == "PAUSAR_PARES" && control.IdsPares is { Count: > 0 })
            {
                await _ipc.LimpiarComandoAsync(_opciones.NombrePerfil, cancelacion).ConfigureAwait(false);
                lock (_estado.CandadoPares)
                {
                    foreach (var par in _estado.Pares.Where(p => control.IdsPares.Contains(p.IdPar, StringComparer.OrdinalIgnoreCase)))
                    {
                        par.Pausado = true;
                        _estado.EncolarLog(par.IdPar, "INFO", $"Par '{par.Nombre}' pausado.");
                    }
                }
            }
        }

        DrenarLineasLogAlDisco();
        DetectarCambioConfig();
        await ProcesarRecargaPendienteAsync(cancelacion).ConfigureAwait(false);
        ProcesarEstadisticas();
        ProcesarPollingSeguridad();

        _estado.DrenarActividadAlHistorial();
        var estadoIpc = ConstruirEstadoIpc();
        await _ipc.PublicarEstadoAsync(estadoIpc, cancelacion).ConfigureAwait(false);

        await Task.Delay(intervaloMs, cancelacion).ConfigureAwait(false);
        return true;
    }

    private void SolicitarApagadoInmediato()
    {
        if (_estado is null)
        {
            return;
        }

        _estado.AceptarNuevosTrabajos = false;
        _estado.EnEjecucion = false;
        _estado.SolicitudParada = true;
        _estado.SolicitudParadaCopiadores = true;
        _estado.SolicitudParadaHidratadores = true;
        _estado.ColaCopia.CompletarEscritura();
        _estado.ColaHidratacion.CompletarEscritura();
        _estado.EncolarLog("__ALL__", "INFO", "Apagado solicitado: deteniendo trabajos en curso");
    }

    private async Task DetenerWorkersSinBucleAsync()
    {
        if (_gestorVigias is not null)
        {
            await _gestorVigias.DetenerTodosAsync().ConfigureAwait(false);
        }

        if (_poolCopiadores is not null)
        {
            await _poolCopiadores.DetenerAsync(TimeSpan.FromMinutes(2)).ConfigureAwait(false);
        }

        if (_poolHidratadores is not null)
        {
            await _poolHidratadores.DetenerAsync().ConfigureAwait(false);
        }
    }

    private void ProcesarPollingSeguridad()
    {
        if (_estado is null)
        {
            return;
        }

        List<ParSincronizacion> candidatos;
        lock (_estado.CandadoPares)
        {
            candidatos = _estado.Pares
                .Where(p => p.Habilitado && !p.Pausado)
                .ToList();
        }

        var ahora = DateTime.UtcNow;
        foreach (var par in candidatos)
        {
            if (!_estado.ProximoPollingPorParUtc.TryGetValue(par.IdPar, out var proximo) || ahora < proximo)
            {
                continue;
            }

            _estado.ProgramarProximoPolling(par);
            _estado.SolicitarEscaneoPorPolling(par.IdPar);
        }
    }

    private void InicializarTemporizadoresPolling(ConfiguracionAplicacion config)
    {
        if (_estado is null)
        {
            return;
        }

        _estado.ProximoPollingPorParUtc.Clear();
        foreach (var par in config.Pares.Where(p => p.Habilitado && !p.Pausado))
        {
            _estado.ProgramarProximoPolling(par);
        }
    }

    private void SincronizarTemporizadoresPollingTrasRecarga()
    {
        if (_estado is null)
        {
            return;
        }

        var idsActivos = _estado.Pares
            .Where(p => p.Habilitado && !p.Pausado)
            .Select(p => p.IdPar)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var id in _estado.ProximoPollingPorParUtc.Keys.ToList())
        {
            if (!idsActivos.Contains(id))
            {
                _estado.ProximoPollingPorParUtc.TryRemove(id, out _);
                _estado.EscaneoPollingPendientePorPar.TryRemove(id, out _);
            }
        }

        foreach (var par in _estado.Pares.Where(p => p.Habilitado && !p.Pausado))
        {
            if (!_estado.ProximoPollingPorParUtc.ContainsKey(par.IdPar))
            {
                _estado.ProgramarProximoPolling(par);
            }
        }
    }

    private void ProcesarEstadisticas()
    {
        if (_estado is null)
        {
            return;
        }

        while (_estado.ColaEstadisticas.TryDequeue(out var stat))
        {
            if (!_resumenPares.TryGetValue(stat.IdPar, out var resumen))
            {
                continue;
            }

            resumen.Copiados += stat.Copiados;
            resumen.Errores += stat.Errores;
            resumen.Estado = stat.Estado;
            resumen.UltimaSincronizacion = stat.UltimaSincronizacion?.ToString("o");
        }
    }

    private void DetectarCambioConfig()
    {
        try
        {
            var marca = File.GetLastWriteTimeUtc(_opciones.RutaConfiguracion);
            if (_ultimaMarcaConfig is null || marca != _ultimaMarcaConfig)
            {
                _recargaPendienteDesde ??= DateTime.UtcNow;
            }
        }
        catch
        {
            // Ignorar errores de lectura puntual.
        }
    }

    private async Task ProcesarRecargaPendienteAsync(CancellationToken cancelacion)
    {
        if (_recargaPendienteDesde is null || _estado is null)
        {
            return;
        }

        if ((DateTime.UtcNow - _recargaPendienteDesde.Value).TotalSeconds < 1.5)
        {
            return;
        }

        _recargaPendienteDesde = null;

        try
        {
            var nueva = await _repositorioConfig.LeerAsync(_opciones.RutaConfiguracion, cancelacion)
                .ConfigureAwait(false);
            var validacion = _validador.Validar(nueva);
            if (!validacion.Valida)
            {
                foreach (var err in validacion.Errores)
                {
                    _estado.EncolarLog("__ALL__", "WARN", $"Recarga config rechazada: {err}");
                }

                return;
            }

            var origenesAnteriores = _estado.Pares.ToDictionary(p => p.IdPar, p => p.RutaOrigen, StringComparer.OrdinalIgnoreCase);
            FusionarContadores(nueva);

            lock (_estado.CandadoPares)
            {
                _estado.Config = nueva;
                _estado.Pares = nueva.Pares.ToList();
            }

            var idsActuales = _estado.Pares.Select(p => p.IdPar).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var idAntiguo in origenesAnteriores.Keys.Where(k => !idsActuales.Contains(k)).ToList())
            {
                if (_gestorVigias is not null)
                {
                    await _gestorVigias.DetenerVigiaAsync(idAntiguo).ConfigureAwait(false);
                }
            }

            foreach (var par in _estado.Pares.Where(p => p.Habilitado))
            {
                if (!origenesAnteriores.ContainsKey(par.IdPar)
                    || !string.Equals(origenesAnteriores[par.IdPar], par.RutaOrigen, StringComparison.OrdinalIgnoreCase))
                {
                    if (_gestorVigias is not null)
                    {
                        await _gestorVigias.ReiniciarVigiaAsync(par.IdPar).ConfigureAwait(false);
                    }
                }
            }

            _ultimaMarcaConfig = File.GetLastWriteTimeUtc(_opciones.RutaConfiguracion);

            SincronizarTemporizadoresPollingTrasRecarga();

            // Mantener copia espejo que usa el demonio al arrancar.
            var rutaActiva = RutasDatos.ObtenerRutaConfiguracionActiva(_opciones.NombrePerfil);
            await CopiaArchivoConReintentos
                .CopiarAsync(_opciones.RutaConfiguracion, rutaActiva, cancelacion: cancelacion)
                .ConfigureAwait(false);

            // Filtros o rutas pueden haber cambiado: forzar escaneo en todos los pares activos.
            lock (_estado.CandadoPares)
            {
                foreach (var par in _estado.Pares.Where(p => p.Habilitado && !p.Pausado))
                {
                    _estado.PeticionEscaneoCompleto[par.IdPar] = true;
                }
            }

            _estado.EncolarLog("__ALL__", "INFO", "Configuración recargada en caliente");
            _logger.LogInformation("Configuración recargada para perfil {Perfil}", _opciones.NombrePerfil);
        }
        catch (Exception ex)
        {
            _estado.EncolarLog("__ALL__", "WARN", $"Error en recarga: {ex.Message}");
        }
    }

    private void FusionarContadores(ConfiguracionAplicacion nueva)
    {
        foreach (var par in nueva.Pares)
        {
            if (_resumenPares.TryGetValue(par.IdPar, out var resumen))
            {
                par.TotalCopiados = resumen.Copiados;
                par.TotalErrores = resumen.Errores;
            }
        }

        InicializarResumenPares(nueva.Pares);
    }

    private void InicializarResumenPares(IReadOnlyList<ParSincronizacion>? pares = null)
    {
        pares ??= _estado?.Pares ?? [];
        foreach (var par in pares)
        {
            _resumenPares[par.IdPar] = new ResumenParInterno
            {
                IdPar = par.IdPar,
                Nombre = par.Nombre,
                Copiados = par.TotalCopiados,
                Errores = par.TotalErrores,
                Estado = par.Pausado ? "PAUSADO" : "OK"
            };
        }
    }

    private EstadoPerfil ConstruirEstadoIpc()
    {
        var estado = _estado!;
        var segundosPolling = estado.SegundosHastaProximoPollingGlobal();

        List<EntradaActividad> actividad;
        lock (estado.CandadoHistorial)
        {
            actividad = estado.HistorialActividad
                .Select(a => new EntradaActividad
                {
                    Hora = a.Hora,
                    Tipo = a.Tipo,
                    Archivo = a.Archivo,
                    IdPar = a.IdPar
                })
                .ToList();
        }

        var copiasEnCurso = estado.CopiasEnCurso.Values
            .Select(CrearCopiaEnCursoIpc)
            .ToList();

        var pares = _resumenPares.Values.Select(r =>
        {
            var tieneBloqueo = estado.PurgasMasivasBloqueadas.TryGetValue(r.IdPar, out var conteo);
            return new ResumenPar
            {
                IdPar = r.IdPar,
                Nombre = r.Nombre,
                Estado = r.Estado,
                Copiados = r.Copiados,
                Errores = r.Errores,
                UltimaSincronizacion = r.UltimaSincronizacion,
                ProximoPollingEnSegundos = estado.SegundosHastaProximoPolling(r.IdPar),
                PurgaMasivaBloqueada = tieneBloqueo,
                ArchivosPurgaBloqueados = tieneBloqueo ? conteo : 0
            };
        }).ToList();

        var (memoriaBytes, cpuPorcentaje) = _muestreadorRecursos.Muestrear();

        return new EstadoPerfil
        {
            Perfil = _opciones.NombrePerfil,
            Pid = Environment.ProcessId,
            EnEjecucion = estado.EnEjecucion,
            AceptarNuevosTrabajos = estado.AceptarNuevosTrabajos,
            ProximoPollingEnSegundos = segundosPolling,
            ColaCopiaPendiente = estado.ColaCopia.PendientesEnCola,
            ArchivosUnicosPendientes = estado.ColaCopia.PendientesEnCola,
            DuplicadosEvitados = estado.Metricas.DuplicadosEvitados,
            HidratacionesActivas = estado.HidratacionesActivas.Count,
            InicioSesionUtc = estado.InicioSesionUtc.ToString("o"),
            Totales = new TotalesEstado
            {
                Copiados = estado.Metricas.TotalCopiados,
                Errores = estado.Metricas.TotalErrores,
                BytesEscritos = estado.Metricas.BytesEscritos
            },
            Recursos = new RecursosProceso
            {
                MemoriaTrabajoBytes = memoriaBytes,
                CpuPorcentaje = cpuPorcentaje
            },
            Pares = pares,
            ActividadReciente = actividad,
            CopiasEnCurso = copiasEnCurso
        };
    }

    private async Task PublicarEstadoFinalAsync(CancellationToken cancelacion)
    {
        try
        {
            var estado = ConstruirEstadoIpc();
            estado.EnEjecucion = false;
            await _ipc.PublicarEstadoAsync(estado, cancelacion).ConfigureAwait(false);
        }
        catch
        {
            // No tumbar apagado por IPC.
        }
    }

    private IReadOnlyList<string> DrenarLineasLog()
    {
        if (_estado is null)
        {
            return [];
        }

        var lineas = new List<string>();
        while (_estado.ColaLogs.TryDequeue(out var linea))
        {
            var partes = linea.Split('|', 3);
            if (partes.Length < 3)
            {
                continue;
            }

            var marca = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var etiquetaPar = partes[0] == "__ALL__"
                ? "*"
                : ResolverNombreParParaLog(partes[0]);
            var lineaFormateada =
                $"[{marca}] [perfil:{_opciones.NombrePerfil}] [par:{etiquetaPar}] [{partes[1]}] {partes[2]}";
            lineas.Add(lineaFormateada);
            _estado.ColaDisco.Enqueue(lineaFormateada);
        }

        return lineas;
    }

    private static CopiaEnCurso CrearCopiaEnCursoIpc(CopiaEnCursoInterna copia)
    {
        var porcentaje = copia.BytesTotales > 0
            ? (int)Math.Clamp(copia.BytesCopiados * 100 / copia.BytesTotales, 0, 100)
            : 0;

        int? etaSegundos = null;
        if (copia.BytesTotales > 0
            && copia.BytesCopiados > 0
            && copia.BytesCopiados < copia.BytesTotales
            && copia.InicioUtc != default)
        {
            var transcurrido = (DateTime.UtcNow - copia.InicioUtc).TotalSeconds;
            if (transcurrido > 0.5)
            {
                var velocidad = copia.BytesCopiados / transcurrido;
                if (velocidad > 0)
                {
                    etaSegundos = (int)Math.Ceiling((copia.BytesTotales - copia.BytesCopiados) / velocidad);
                }
            }
        }

        return new CopiaEnCurso
        {
            Archivo = copia.Archivo,
            IdPar = copia.IdPar,
            Copiador = copia.Copiador,
            BytesTotales = copia.BytesTotales,
            BytesCopiados = copia.BytesCopiados,
            Porcentaje = porcentaje,
            EtaSegundos = etaSegundos
        };
    }

    /// <summary>Resuelve el nombre legible del par; si no existe, conserva el IdPar.</summary>
    private string ResolverNombreParParaLog(string idPar)
    {
        if (_estado is null)
        {
            return idPar;
        }

        lock (_estado.CandadoPares)
        {
            var par = _estado.Pares.FirstOrDefault(p =>
                string.Equals(p.IdPar, idPar, StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(par?.Nombre) ? idPar : par.Nombre;
        }
    }

    private void DrenarLineasLogAlDisco()
    {
        _ = DrenarLineasLog();
    }

    public async ValueTask DisposeAsync()
    {
        if (_ctsPrincipal is not null)
        {
            await DetenerOrdenadoAsync(CancellationToken.None).ConfigureAwait(false);
            _ctsPrincipal.Dispose();
        }

        if (_gestorVigias is not null)
        {
            await _gestorVigias.DisposeAsync().ConfigureAwait(false);
        }

        if (_poolCopiadores is not null)
        {
            await _poolCopiadores.DisposeAsync().ConfigureAwait(false);
        }

        if (_poolHidratadores is not null)
        {
            await _poolHidratadores.DisposeAsync().ConfigureAwait(false);
        }

        if (_escritorLog is not null)
        {
            await _escritorLog.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string FormatearErrorBuclePrincipal(Exception ex)
    {
        var mensajeHumano = ServicioMensajesErrorHumano.TraducirExcepcion(ex);
        var texto =
            $"Error en el ciclo de control del demonio: {mensajeHumano} El demonio sigue activo e intentará de nuevo.";

        if (ex is UnauthorizedAccessException or IOException)
        {
            texto += " Comprueba permisos en %LOCALAPPDATA%\\SManager2.";
        }

        return texto;
    }

    private sealed class ResumenParInterno
    {
        public string IdPar { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Estado { get; set; } = "OK";
        public int Copiados { get; set; }
        public int Errores { get; set; }
        public string? UltimaSincronizacion { get; set; }
    }
}
