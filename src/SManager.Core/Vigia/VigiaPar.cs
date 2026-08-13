using SManager.Core.Copia;
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
    private static readonly TimeSpan IntervaloAvisoColaLlena = TimeSpan.FromSeconds(60);

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
    private readonly Dictionary<string, DateTime> _ultimoAvisoColaLlena = new(StringComparer.OrdinalIgnoreCase);

    private enum ResultadoRegistro
    {
        Ignorado,
        YaSincronizado,
        Candidato
    }

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
                        var esPolling = solicitaEscaneo
                            && _estado.PeticionEscaneoPorPolling.TryGetValue(_idPar, out var porPolling)
                            && porPolling;

                        await EjecutarEscaneoCompletoConColaAsync(
                                par,
                                cancelacion,
                                esPolling: esPolling,
                                esRafaga: rafaga >= UmbralRafaga,
                                eventosRafaga: rafaga)
                            .ConfigureAwait(false);
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
            // 64 KB: límite seguro en Windows; reduce pérdida de eventos en copias masivas.
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

        _vigilante.Deleted += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Name) && par.Modo == ModoSincronizacion.Espejo)
            {
                ProcesarBorradoEspejo(par, Path.Combine(par.RutaOrigen, e.Name));
            }
        };

        _origenVigilante = origenNorm;
        _estado.EncolarLog(_idPar, "INFO", $"Vigía activo: {par.RutaOrigen}");
    }

    private void ProcesarBorradoEspejo(ParSincronizacion par, string rutaOrigenCompleta)
    {
        var rutaDestino = ComparadorSincronizacion.ObtenerRutaDestino(par, rutaOrigenCompleta);
        if (string.IsNullOrEmpty(rutaDestino))
        {
            return;
        }

        try
        {
            if (File.Exists(rutaDestino))
            {
                File.Delete(rutaDestino);
                _estado.EncolarLog(_idPar, "INFO", $"[ESPEJO] Archivo eliminado en destino por borrado en origen: {Path.GetFileName(rutaDestino)}");
                _estado.RegistrarActividad("BORRADO_ESPEJO", Path.GetFileName(rutaDestino), _idPar);
            }
            else if (Directory.Exists(rutaDestino))
            {
                Directory.Delete(rutaDestino, recursive: true);
                _estado.EncolarLog(_idPar, "INFO", $"[ESPEJO] Carpeta eliminada en destino por borrado en origen: {Path.GetFileName(rutaDestino)}");
            }
        }
        catch (Exception ex)
        {
            _estado.EncolarLog(_idPar, "ERROR", $"[ESPEJO] Error al replicar borrado en destino ({rutaDestino}): {ex.Message}");
        }
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

    /// <returns>Clasificación del archivo tras filtros y comparación con destino.</returns>
    private ResultadoRegistro RegistrarCandidato(
        string rutaCompleta,
        ParSincronizacion par,
        IndiceMetadatosDestino? indiceDestino = null)
    {
        if (!_estado.AceptarNuevosTrabajos)
        {
            return ResultadoRegistro.Ignorado;
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

                return ResultadoRegistro.Ignorado;
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

                return ResultadoRegistro.Candidato;
            }

            return ResultadoRegistro.Ignorado;
        }

        if (info.Attributes.HasFlag(FileAttributes.Directory))
        {
            return ResultadoRegistro.Ignorado;
        }

        if (!ServicioFiltros.PasaFiltros(info.Name, par))
        {
            return ResultadoRegistro.Ignorado;
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

            return ResultadoRegistro.Candidato;
        }

        // Escaneo/polling: omitir archivos ya alineados con el destino (evita llenar la cola).
        if (!ComparadorSincronizacion.NecesitaCopia(info, par, indiceDestino))
        {
            lock (_candadoPendientes)
            {
                _pendientes.Remove(info.FullName);
            }

            return ResultadoRegistro.YaSincronizado;
        }

        lock (_candadoPendientes)
        {
            if (_pendientes.TryGetValue(info.FullName, out var prev)
                && prev.Tamano == info.Length
                && prev.MtimeUtc == info.LastWriteTimeUtc)
            {
                return ResultadoRegistro.Candidato;
            }

            _pendientes[info.FullName] = new SeguimientoEstabilidad
            {
                Tamano = info.Length,
                MtimeUtc = info.LastWriteTimeUtc
            };
        }

        return ResultadoRegistro.Candidato;
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

                    if (!ComparadorSincronizacion.NecesitaCopia(info, par))
                    {
                        aEliminar.Add(clave);
                        continue;
                    }

                    var resultado = _estado.ColaCopia.IntentarEncolar(
                        new TrabajoCopia(par.IdPar, clave),
                        _estado.Metricas);

                    switch (resultado)
                    {
                        case ResultadoEncoladoCopia.Encolado:
                        case ResultadoEncoladoCopia.DuplicadoEnCola:
                            aEliminar.Add(clave);
                            break;
                        case ResultadoEncoladoCopia.ColaLlena:
                            RegistrarAvisoColaLlena(clave);
                            break;
                    }
                }
            }

            foreach (var k in aEliminar)
            {
                _pendientes.Remove(k);
            }
        }
    }

    /// <summary>
    /// Ejecuta escaneo completo y, si llegaron pollings durante la ejecución,
    /// lanza como mucho un barrido adicional encolado (coalescencia).
    /// </summary>
    private async Task EjecutarEscaneoCompletoConColaAsync(
        ParSincronizacion par,
        CancellationToken cancelacion,
        bool esPolling,
        bool esRafaga,
        int eventosRafaga)
    {
        var esPrimeraPasada = true;

        while (true)
        {
            if (esPrimeraPasada)
            {
                if (esPolling)
                {
                    _estado.EncolarLog(_idPar, "INFO", "Polling de seguridad: escaneo diferencial");
                    _estado.PeticionEscaneoCompleto[_idPar] = false;
                    _estado.PeticionEscaneoPorPolling.TryRemove(_idPar, out _);
                }
                else if (_estado.PeticionEscaneoCompleto.TryGetValue(_idPar, out var scan) && scan)
                {
                    _estado.EncolarLog(_idPar, "INFO", "Escaneo completo solicitado");
                    _estado.PeticionEscaneoCompleto[_idPar] = false;
                }
                else if (esRafaga)
                {
                    _estado.EncolarLog(_idPar, "INFO", $"Ráfaga FSW ({eventosRafaga} eventos): escaneo de rescate");
                }
            }
            else
            {
                _estado.EncolarLog(_idPar, "INFO", "Polling encolado: ejecutando escaneo pendiente");
            }

            _estado.EscaneoEnCursoPorPar[_idPar] = true;
            try
            {
                await EscaneoCompletoAsync(par, cancelacion).ConfigureAwait(false);
            }
            finally
            {
                _estado.EscaneoEnCursoPorPar[_idPar] = false;
            }

            if (!_estado.EscaneoPollingPendientePorPar.TryRemove(_idPar, out var pendiente) || !pendiente)
            {
                break;
            }

            esPrimeraPasada = false;
            esPolling = true;
            esRafaga = false;
        }
    }

    private async Task EscaneoCompletoAsync(ParSincronizacion par, CancellationToken cancelacion)
    {
        if (!Directory.Exists(par.RutaOrigen))
        {
            _estado.EncolarLog(_idPar, "ERROR", $"Origen inaccesible: {par.RutaOrigen}");
            return;
        }

        _estado.EncolarLog(_idPar, "INFO", "Escaneo diferencial (origen vs destino)...");

        var inicioIndice = DateTime.UtcNow;
        IndiceMetadatosDestino? indiceDestino = null;
        try
        {
            indiceDestino = IndiceMetadatosDestino.Construir(par, cancelacion);
            var segundosIndice = (DateTime.UtcNow - inicioIndice).TotalSeconds;
            _estado.EncolarLog(
                _idPar,
                "INFO",
                $"Índice de destino: {indiceDestino.CantidadArchivos} archivos ({segundosIndice:F1} s)");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _estado.EncolarLog(
                _idPar,
                "WARN",
                $"Índice de destino no disponible; comparación archivo a archivo: {ex.Message}");
        }

        var revisados = 0;
        var candidatos = 0;
        var yaSincronizados = 0;
        var ignorados = 0;

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
                switch (RegistrarCandidato(archivo, par, indiceDestino))
                {
                    case ResultadoRegistro.Candidato:
                        candidatos++;
                        break;
                    case ResultadoRegistro.YaSincronizado:
                        yaSincronizados++;
                        break;
                    default:
                        ignorados++;
                        break;
                }
            }
            catch (Exception ex)
            {
                _estado.EncolarLog(_idPar, "ERROR", $"Escaneo: {ex.Message}");
            }
        }

        if (par.Modo == ModoSincronizacion.Espejo && Directory.Exists(par.RutaDestino) && Directory.Exists(par.RutaOrigen))
        {
            try
            {
                var rutaRaizDestino = Path.GetFullPath(par.RutaDestino.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var rutaRaizOrigen = Path.GetFullPath(par.RutaOrigen.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

                var huerfanos = new List<(string RutaAbsoluta, string RutaRelativa)>();
                foreach (var archivoDestino in Directory.EnumerateFiles(par.RutaDestino, "*", SearchOption.AllDirectories))
                {
                    cancelacion.ThrowIfCancellationRequested();
                    var rel = archivoDestino[rutaRaizDestino.Length..].TrimStart('\\', '/');
                    var equivOrigen = Path.Combine(rutaRaizOrigen, rel);

                    if (!File.Exists(equivOrigen) && ServicioFiltros.PasaFiltros(Path.GetFileName(archivoDestino), par))
                    {
                        huerfanos.Add((archivoDestino, rel));
                    }
                }

                var umbralSeguridad = Math.Max(1, par.UmbralPurgaMasivaEspejo <= 0 ? 50 : par.UmbralPurgaMasivaEspejo);
                var fueAutorizadaConscientemente = _estado.AutorizacionPurgaMasivaUnaVez.TryRemove(_idPar, out var autorizada) && autorizada;

                // Guardián Antidesastre: Si la purga supera el umbral de seguridad y NO ha sido autorizada conscientemente
                if (huerfanos.Count > umbralSeguridad && !fueAutorizadaConscientemente)
                {
                    _estado.PurgasMasivasBloqueadas[_idPar] = huerfanos.Count;
                    _estado.EncolarLog(
                        _idPar,
                        "WARN",
                        $"[ALERTA ESPEJO] Purga masiva detenida por seguridad: {huerfanos.Count} archivos superan el umbral ({umbralSeguridad}). Usa 'smanager autorizar-purga' o el botón en la GUI para autorizarla.");
                }
                else
                {
                    _estado.PurgasMasivasBloqueadas.TryRemove(_idPar, out _);
                    if (fueAutorizadaConscientemente && huerfanos.Count > umbralSeguridad)
                    {
                        _estado.EncolarLog(_idPar, "INFO", $"[ESPEJO] Ejecutando purga masiva intencionada autorizada por el usuario ({huerfanos.Count} archivos).");
                    }

                    foreach (var (archivoDestino, rel) in huerfanos)
                    {
                        cancelacion.ThrowIfCancellationRequested();
                        try
                        {
                            File.Delete(archivoDestino);
                            _estado.EncolarLog(_idPar, "INFO", $"[ESPEJO] Purga de archivo huérfano en destino: {rel}");
                            _estado.RegistrarActividad("PURGA_ESPEJO", rel, _idPar);
                        }
                        catch (Exception exPurga)
                        {
                            _estado.EncolarLog(_idPar, "ERROR", $"[ESPEJO] Error al purgar en destino '{rel}': {exPurga.Message}");
                        }
                    }
                }
            }
            catch (Exception exEspejo)
            {
                _estado.EncolarLog(_idPar, "ERROR", $"[ESPEJO] Error en escaneo espejo: {exEspejo.Message}");
            }
        }

        _estado.EncolarLog(_idPar, "INFO",
            $"Escaneo: {revisados} revisados, {candidatos} pendientes, {yaSincronizados} ya sincronizados, {ignorados} ignorados");
        if (revisados > 0 && candidatos == 0 && yaSincronizados == 0)
        {
            _estado.EncolarLog(_idPar, "WARN",
                $"Ningún archivo pasó el filtro de inclusión '{par.FiltroInclusion}'. Revisa la columna Inclusion en la GUI.");
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>Evita miles de WARN idénticos por segundo cuando la cola está saturada.</summary>
    private void RegistrarAvisoColaLlena(string rutaCompleta)
    {
        var ahora = DateTime.UtcNow;
        lock (_candadoPendientes)
        {
            if (_ultimoAvisoColaLlena.TryGetValue(rutaCompleta, out var previo)
                && ahora - previo < IntervaloAvisoColaLlena)
            {
                return;
            }

            _ultimoAvisoColaLlena[rutaCompleta] = ahora;
        }

        _estado.EncolarLog(
            _idPar,
            "WARN",
            $"Cola de copia llena; reintentando: {Path.GetFileName(rutaCompleta)}");
    }

    public async ValueTask DisposeAsync()
    {
        await DetenerAsync().ConfigureAwait(false);
        _cts.Dispose();
    }
}
