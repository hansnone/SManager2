using Microsoft.Extensions.Logging;
using SManager.Core.Configuracion;
using SManager.Core.Logging;
using SManager.Core.Modelos;
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
        await Task.Run(() => File.Copy(_opciones.RutaConfiguracion, rutaActiva, overwrite: true), cancelacionExterna)
            .ConfigureAwait(false);

        _estado = new EstadoMotor
        {
            Config = config,
            Pares = config.Pares.ToList(),
            EnEjecucion = true,
            ProximoPollingUtc = DateTime.UtcNow.AddSeconds(config.IntervaloPollingSegundos)
        };

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
            await _poolCopiadores.DetenerAsync(TimeSpan.FromMinutes(10)).ConfigureAwait(false);
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
                DrenarLineasLogAlDisco();
                DetectarCambioConfig();
                await ProcesarRecargaPendienteAsync(cancelacion).ConfigureAwait(false);
                ProcesarEstadisticas();
                ProcesarPollingSeguridad();

                var comando = await _ipc.LeerComandoPendienteAsync(_opciones.NombrePerfil, cancelacion)
                    .ConfigureAwait(false);

                if (comando == ComandoControl.Apagar)
                {
                    await _ipc.LimpiarComandoAsync(_opciones.NombrePerfil, cancelacion).ConfigureAwait(false);
                    _logger.LogInformation("Señal APAGAR recibida para perfil {Perfil}", _opciones.NombrePerfil);
                    break;
                }

                if (comando == ComandoControl.Recargar)
                {
                    await _ipc.LimpiarComandoAsync(_opciones.NombrePerfil, cancelacion).ConfigureAwait(false);
                    _recargaPendienteDesde = DateTime.UtcNow.AddSeconds(-10);
                    await ProcesarRecargaPendienteAsync(cancelacion).ConfigureAwait(false);
                }

                _estado.DrenarActividadAlHistorial();
                var estadoIpc = ConstruirEstadoIpc();
                await _ipc.PublicarEstadoAsync(estadoIpc, cancelacion).ConfigureAwait(false);

                await Task.Delay(intervaloMs, cancelacion).ConfigureAwait(false);
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

    private async Task DetenerWorkersSinBucleAsync()
    {
        if (_gestorVigias is not null)
        {
            await _gestorVigias.DetenerTodosAsync().ConfigureAwait(false);
        }

        if (_poolCopiadores is not null)
        {
            await _poolCopiadores.DetenerAsync(TimeSpan.FromMinutes(10)).ConfigureAwait(false);
        }

        if (_poolHidratadores is not null)
        {
            await _poolHidratadores.DetenerAsync().ConfigureAwait(false);
        }
    }

    private void ProcesarPollingSeguridad()
    {
        if (_estado?.ProximoPollingUtc is null || DateTime.UtcNow < _estado.ProximoPollingUtc)
        {
            return;
        }

        lock (_estado.CandadoPares)
        {
            foreach (var par in _estado.Pares.Where(p => p.Habilitado && !p.Pausado))
            {
                _estado.PeticionEscaneoCompleto[par.IdPar] = true;
            }
        }

        _estado.EncolarLog("__ALL__", "INFO", "Polling de seguridad programado");
        _estado.ProximoPollingUtc = DateTime.UtcNow.AddSeconds(_estado.Config.IntervaloPollingSegundos);
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

            // Mantener copia espejo que usa el demonio al arrancar.
            var rutaActiva = RutasDatos.ObtenerRutaConfiguracionActiva(_opciones.NombrePerfil);
            await Task.Run(() => File.Copy(_opciones.RutaConfiguracion, rutaActiva, overwrite: true), cancelacion)
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
        var segundosPolling = estado.ProximoPollingUtc.HasValue
            ? (int)Math.Max(0, (estado.ProximoPollingUtc.Value - DateTime.UtcNow).TotalSeconds)
            : (int?)null;

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
            .Select(c => new CopiaEnCurso
            {
                Archivo = c.Archivo,
                IdPar = c.IdPar,
                Copiador = c.Copiador
            })
            .ToList();

        var pares = _resumenPares.Values.Select(r => new ResumenPar
        {
            IdPar = r.IdPar,
            Nombre = r.Nombre,
            Estado = r.Estado,
            Copiados = r.Copiados,
            Errores = r.Errores,
            UltimaSincronizacion = r.UltimaSincronizacion
        }).ToList();

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
            Totales = new TotalesEstado
            {
                Copiados = estado.Metricas.TotalCopiados,
                Errores = estado.Metricas.TotalErrores
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
