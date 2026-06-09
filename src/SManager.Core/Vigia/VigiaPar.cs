using SManager.Core.Modelos;
using SManager.Core.Motor;
using SManager.Core.Utilidades;

namespace SManager.Core.Vigia;

/// <summary>
/// Vigía por par: FileSystemWatcher + estabilidad + encolado (sin copiar).
/// Un hilo de tarea por par, aislado con bulkhead pattern.
/// No propaga borrados del origen al destino (sincronización unidireccional, no espejo).
/// </summary>
public sealed class VigiaPar : IAsyncDisposable
{
    private const int UmbralRafaga = 80;
    private const int BufferFswBytes = 65536;

    private readonly EstadoMotor _estado;
    private readonly string _idPar;
    private readonly CancellationTokenSource _cts = new();
    private Task? _tarea;
    private FileSystemWatcher? _vigilante;
    private string _origenVigilante = string.Empty;
    private readonly Dictionary<string, SeguimientoEstabilidad> _pendientes = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _candadoPendientes = new();
    private readonly Queue<string> _colaEventos = new();
    private readonly object _candadoEventos = new();

    public string IdPar => _idPar;

    public VigiaPar(EstadoMotor estado, string idPar)
    {
        _estado = estado;
        _idPar = idPar;
    }

    public void Iniciar()
    {
        _estado.ParadasVigiaPorId[_idPar] = false;
        _estado.PeticionEscaneoCompleto[_idPar] = true;
        _tarea = Task.Run(() => EjecutarBucleAsync(_cts.Token), _cts.Token);
    }

    public async Task DetenerAsync()
    {
        _estado.ParadasVigiaPorId[_idPar] = true;
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_tarea is not null)
        {
            try
            {
                await _tarea.WaitAsync(TimeSpan.FromSeconds(8)).ConfigureAwait(false);
            }
            catch
            {
                // Timeout aceptable en apagado.
            }
        }

        LiberarVigilante();
    }

    private async Task EjecutarBucleAsync(CancellationToken cancelacion)
    {
        try
        {
            while (!cancelacion.IsCancellationRequested && !DebeParar())
            {
                var par = ObtenerPar();
                if (par is null || par.Pausado || !par.Habilitado)
                {
                    LiberarVigilante();
                    await Task.Delay(500, cancelacion).ConfigureAwait(false);
                    continue;
                }

                AsegurarVigilante(par);

                var segEstab = Math.Clamp(_estado.Config.SegundosEstabilidadArchivo, 1, 30);
                var rafaga = DrenarEventos(par);

                try
                {
                    var solicitaEscaneo = _estado.PeticionEscaneoCompleto.TryGetValue(_idPar, out var scan) && scan;
                    if (rafaga >= UmbralRafaga || solicitaEscaneo)
                    {
                        if (solicitaEscaneo)
                        {
                            _estado.EncolarLog(_idPar, "INFO", "Polling de seguridad: escaneo completo");
                            _estado.PeticionEscaneoCompleto[_idPar] = false;
                        }
                        else if (rafaga >= UmbralRafaga)
                        {
                            _estado.EncolarLog(_idPar, "INFO", $"Ráfaga FSW ({rafaga} eventos): escaneo completo de rescate");
                        }

                        await EscaneoCompletoAsync(par, cancelacion).ConfigureAwait(false);
                        ProcesarPendientesEstables(par, segEstab);
                    }
                    else
                    {
                        ProcesarPendientesEstables(par, segEstab);
                        var sinPendientes = false;
                        lock (_candadoPendientes)
                        {
                            sinPendientes = _pendientes.Count == 0;
                        }

                        if (sinPendientes && rafaga == 0)
                        {
                            await Task.Delay(50, cancelacion).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _estado.EncolarLog(_idPar, "ERROR", $"Bucle vigía (sigue vivo): {ex.Message}");
                    await Task.Delay(500, cancelacion).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            LiberarVigilante();
        }
    }

    private bool DebeParar() =>
        _estado.SolicitudParada
        || (_estado.ParadasVigiaPorId.TryGetValue(_idPar, out var parar) && parar);

    private ParSincronizacion? ObtenerPar()
    {
        lock (_estado.CandadoPares)
        {
            return _estado.Pares.FirstOrDefault(p => p.IdPar == _idPar);
        }
    }

    private void AsegurarVigilante(ParSincronizacion par)
    {
        var origenNorm = Path.GetFullPath(par.RutaOrigen.Trim());

        if (_vigilante is not null && !string.Equals(_origenVigilante, origenNorm, StringComparison.OrdinalIgnoreCase))
        {
            _estado.EncolarLog(_idPar, "INFO", $"Origen cambió; reiniciando FileSystemWatcher -> {origenNorm}");
            LiberarVigilante();
        }

        if (_vigilante is not null || !Directory.Exists(par.RutaOrigen))
        {
            return;
        }

        _vigilante = new FileSystemWatcher(par.RutaOrigen)
        {
            IncludeSubdirectories = true,
            InternalBufferSize = BufferFswBytes,
            NotifyFilter = NotifyFilters.LastWrite
                | NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.Size
                | NotifyFilters.Attributes,
            EnableRaisingEvents = true
        };

        FileSystemEventHandler manejador = (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Name))
            {
                lock (_candadoEventos)
                {
                    _colaEventos.Enqueue(Path.Combine(par.RutaOrigen, e.Name!));
                }
            }
        };

        _vigilante.Created += manejador;
        _vigilante.Changed += manejador;
        _vigilante.Renamed += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.FullPath))
            {
                lock (_candadoEventos)
                {
                    _colaEventos.Enqueue(e.FullPath);
                }
            }
        };

        _origenVigilante = origenNorm;
        _estado.EncolarLog(_idPar, "INFO", $"Vigía activo: {par.RutaOrigen}");
    }

    private void LiberarVigilante()
    {
        if (_vigilante is null)
        {
            return;
        }

        _vigilante.EnableRaisingEvents = false;
        _vigilante.Dispose();
        _vigilante = null;
        _origenVigilante = string.Empty;
    }

    private int DrenarEventos(ParSincronizacion par)
    {
        var rafaga = 0;
        while (rafaga < UmbralRafaga)
        {
            string? ruta;
            lock (_candadoEventos)
            {
                if (_colaEventos.Count == 0)
                {
                    break;
                }

                ruta = _colaEventos.Dequeue();
            }

            RegistrarCandidato(ruta, par);
            rafaga++;
        }

        return rafaga;
    }

    /// <returns>true si el archivo pasó filtros y quedó pendiente de estabilidad o hidratación.</returns>
    private bool RegistrarCandidato(string rutaCompleta, ParSincronizacion par)
    {
        if (!_estado.AceptarNuevosTrabajos)
        {
            return false;
        }

        FileInfo? info;
        try
        {
            info = new FileInfo(rutaCompleta);
            if (!info.Exists)
            {
                if (File.Exists(rutaCompleta))
                {
                    lock (_candadoPendientes)
                    {
                        _pendientes.TryAdd(rutaCompleta, new SeguimientoEstabilidad());
                    }
                }

                return false;
            }
        }
        catch
        {
            if (File.Exists(rutaCompleta))
            {
                lock (_candadoPendientes)
                {
                    _pendientes.TryAdd(rutaCompleta, new SeguimientoEstabilidad());
                }

                return true;
            }

            return false;
        }

        if (info.Attributes.HasFlag(FileAttributes.Directory))
        {
            return false;
        }

        if (!ServicioFiltros.PasaFiltros(info.Name, par))
        {
            return false;
        }

        if (OneDrivePlaceholder.EsPlaceholder(info.Attributes))
        {
            if (_estado.HidratacionesActivas.TryAdd(info.FullName, 0))
            {
                _estado.EncolarLog(_idPar, "PENDIENTE", $"Hidratación OneDrive: {info.Name}");
                if (!_estado.ColaHidratacion.IntentarEncolar(new TrabajoHidratacion(par.IdPar, info.FullName)))
                {
                    _estado.HidratacionesActivas.TryRemove(info.FullName, out _);
                    _estado.EncolarLog(_idPar, "WARN", $"Cola de hidratación llena; reintentando: {info.Name}");
                }
            }

            return true;
        }

        lock (_candadoPendientes)
        {
            if (_pendientes.TryGetValue(info.FullName, out var prev)
                && prev.Tamano == info.Length
                && prev.MtimeUtc == info.LastWriteTimeUtc)
            {
                return true;
            }

            _pendientes[info.FullName] = new SeguimientoEstabilidad
            {
                Tamano = info.Length,
                MtimeUtc = info.LastWriteTimeUtc
            };
        }

        return true;
    }

    private void ProcesarPendientesEstables(ParSincronizacion par, double segundosRequeridos)
    {
        var ahora = DateTime.UtcNow;
        List<string> aEliminar;

        lock (_candadoPendientes)
        {
            aEliminar = [];
            foreach (var clave in _pendientes.Keys.ToList())
            {
                if (_estado.SolicitudParada)
                {
                    break;
                }

                FileInfo info;
                try
                {
                    info = new FileInfo(clave);
                    if (!info.Exists)
                    {
                        aEliminar.Add(clave);
                        continue;
                    }
                }
                catch
                {
                    if (File.Exists(clave))
                    {
                        _pendientes[clave].EstableDesdeUtc = null;
                    }
                    else
                    {
                        aEliminar.Add(clave);
                    }

                    continue;
                }

                if (!ServicioFiltros.PasaFiltros(info.Name, par))
                {
                    aEliminar.Add(clave);
                    continue;
                }

                var est = _pendientes[clave];
                if (est.Tamano == -1
                    || info.Length != est.Tamano
                    || info.LastWriteTimeUtc != est.MtimeUtc)
                {
                    _pendientes[clave] = new SeguimientoEstabilidad
                    {
                        Tamano = info.Length,
                        MtimeUtc = info.LastWriteTimeUtc,
                        EstableDesdeUtc = ahora
                    };
                    continue;
                }

                est.EstableDesdeUtc ??= ahora;
                var segEstable = (ahora - est.EstableDesdeUtc.Value).TotalSeconds;
                if (segEstable >= segundosRequeridos)
                {
                    if (!_estado.AceptarNuevosTrabajos)
                    {
                        continue;
                    }

                    var encolado = _estado.ColaCopia.IntentarEncolar(
                        new TrabajoCopia(par.IdPar, clave),
                        _estado.Metricas);

                    if (encolado)
                    {
                        aEliminar.Add(clave);
                    }
                    else
                    {
                        _estado.EncolarLog(
                            _idPar,
                            "WARN",
                            $"Cola de copia llena; reintentando: {Path.GetFileName(clave)}");
                    }
                }
            }

            foreach (var k in aEliminar)
            {
                _pendientes.Remove(k);
            }
        }
    }

    private async Task EscaneoCompletoAsync(ParSincronizacion par, CancellationToken cancelacion)
    {
        if (!Directory.Exists(par.RutaOrigen))
        {
            _estado.EncolarLog(_idPar, "ERROR", $"Origen inaccesible: {par.RutaOrigen}");
            return;
        }

        _estado.EncolarLog(_idPar, "INFO", "Escaneo diferencial (solo encolado)...");
        var revisados = 0;
        var candidatos = 0;

        foreach (var archivo in Directory.EnumerateFiles(par.RutaOrigen, "*", SearchOption.AllDirectories))
        {
            cancelacion.ThrowIfCancellationRequested();
            if (_estado.SolicitudParada)
            {
                break;
            }

            try
            {
                revisados++;
                if (RegistrarCandidato(archivo, par))
                {
                    candidatos++;
                }
            }
            catch (Exception ex)
            {
                _estado.EncolarLog(_idPar, "ERROR", $"Escaneo: {ex.Message}");
            }
        }

        var ignorados = revisados - candidatos;
        _estado.EncolarLog(_idPar, "INFO",
            $"Escaneo: {revisados} revisados, {candidatos} candidatos, {ignorados} ignorados (filtro/carpetas)");
        if (revisados > 0 && candidatos == 0)
        {
            _estado.EncolarLog(_idPar, "WARN",
                $"Ningún archivo pasó el filtro de inclusión '{par.FiltroInclusion}'. Revisa la columna Inclusion en la GUI.");
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await DetenerAsync().ConfigureAwait(false);
        _cts.Dispose();
    }
}
