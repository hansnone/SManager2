using System.Text;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using SManager.Gui.WinUI.Models;
using SManager.Gui.WinUI.Servicios;
using SManager.Ipc;
using SManager.Ipc.Modelos;

namespace SManager.Gui.WinUI.ViewModels;

/// <summary>
/// Parte del ViewModel encargada de la telemetría en tiempo real (Monitor, Estadísticas, Dashboard de Inicio),
/// lectura y filtrado incremental del archivo de registro (smanager.log) y cálculo de tamaños en disco.
/// </summary>
public partial class MainPageViewModel
{
    [RelayCommand]
    private void AplicarFiltroRegistroRapido(string? nivel)
    {
        NivelFiltroRegistroSeleccionado = string.IsNullOrWhiteSpace(nivel)
            ? MapeadorNivelRegistro.EtiquetaTodosLosNiveles
            : nivel;
    }

    [RelayCommand]
    private async Task ExportarRegistroAsync()
    {
        if (LineasRegistro.Count == 0)
        {
            await MostrarAvisoAsync("No hay líneas visibles para exportar.", "Registro vacío");
            return;
        }

        var lineas = LineasRegistro.Select(l => l.TextoCompleto).ToList();
        var ruta = await ServicioExportarDiagnostico.ExportarRegistroAsync(lineas, PerfilActual())
            .ConfigureAwait(true);
        await MostrarAvisoAsync($"Registro exportado en:\n{ruta}", "Exportación completada");
    }

    [RelayCommand]
    private async Task ExportarDiagnosticoAsync()
    {
        var perfil = PerfilActual();
        var estado = await _ipc.LeerEstadoAsync(perfil).ConfigureAwait(true);
        var ruta = await ServicioExportarDiagnostico.ExportarPaqueteDiagnosticoAsync(
                perfil,
                RutaConfiguracion,
                estado,
                _textoRegistroCrudo,
                LineasRegistro.Count)
            .ConfigureAwait(true);

        await MostrarAvisoAsync($"Diagnóstico exportado en:\n{ruta}", "Exportación completada");
    }

    private void ActualizarResumenRegistroFiltrado()
    {
        if (LineasRegistro.Count == 0)
        {
            TextoResumenRegistroFiltrado = string.Empty;
            return;
        }

        var errores = LineasRegistro.Count(l => l.Nivel.Equals("ERROR", StringComparison.OrdinalIgnoreCase));
        var advertencias = LineasRegistro.Count(l => l.Nivel.Equals("WARN", StringComparison.OrdinalIgnoreCase));
        TextoResumenRegistroFiltrado =
            $"{LineasRegistro.Count:N0} líneas visibles — {errores:N0} errores — {advertencias:N0} advertencias";
    }

    private void CargarPreferenciasColumnas()
    {
        _preferenciasColumnas = ServicioPreferenciasColumnas.Cargar();
        AnchoRegistroHora = _preferenciasColumnas.RegistroHora;
        AnchoRegistroPar = _preferenciasColumnas.RegistroPar;
        AnchoRegistroNivel = _preferenciasColumnas.RegistroNivel;
        AnchoRegistroMensaje = _preferenciasColumnas.RegistroMensaje;
        AnchoMonitorNombre = _preferenciasColumnas.MonitorNombre;
        AnchoMonitorEstado = _preferenciasColumnas.MonitorEstado;
        AnchoMonitorTamanoDestino = _preferenciasColumnas.MonitorTamanoDestino;
        AnchoMonitorCopiados = _preferenciasColumnas.MonitorCopiados;
        AnchoMonitorErrores = _preferenciasColumnas.MonitorErrores;
    }

    /// <summary>Persiste anchos de columnas tras arrastrar un separador.</summary>
    public void GuardarPreferenciasColumnas()
    {
        _preferenciasColumnas.RegistroHora = AnchoRegistroHora;
        _preferenciasColumnas.RegistroPar = AnchoRegistroPar;
        _preferenciasColumnas.RegistroNivel = AnchoRegistroNivel;
        _preferenciasColumnas.RegistroMensaje = AnchoRegistroMensaje;
        _preferenciasColumnas.MonitorNombre = AnchoMonitorNombre;
        _preferenciasColumnas.MonitorEstado = AnchoMonitorEstado;
        _preferenciasColumnas.MonitorTamanoDestino = AnchoMonitorTamanoDestino;
        _preferenciasColumnas.MonitorCopiados = AnchoMonitorCopiados;
        _preferenciasColumnas.MonitorErrores = AnchoMonitorErrores;
        ServicioPreferenciasColumnas.Guardar(_preferenciasColumnas);
    }

    /// <summary>Recalcula el tamaño en disco de cada carpeta destino en segundo plano.</summary>
    [RelayCommand]
    private Task ActualizarTamanosDestinoAsync() =>
        ActualizarTamanosDestinoInternoAsync(forzar: true);

    private async Task ActualizarTamanosDestinoInternoAsync(bool forzar = false)
    {
        if (!forzar)
        {
            var transcurrido = DateTimeOffset.UtcNow - _ultimoRecalculoTamanosAutomatico;
            if (transcurrido < IntervaloMinimoRecalculoAutomatico)
            {
                return;
            }
        }

        _cancelacionTamanosDestino?.Cancel();
        _cancelacionTamanosDestino = new CancellationTokenSource();
        var cancelacion = _cancelacionTamanosDestino.Token;

        CalculandoTamanosDestino = true;

        try
        {
            foreach (var par in Pares.ToList())
            {
                cancelacion.ThrowIfCancellationRequested();

                par.TamanoDestinoCalculando = true;
                par.TamanoDestinoTexto = "Calculando…";

                try
                {
                    if (string.IsNullOrWhiteSpace(par.RutaDestino) || !Directory.Exists(par.RutaDestino))
                    {
                        par.TamanoDestinoBytes = 0;
                        par.TamanoDestinoTexto = "No existe";
                    }
                    else
                    {
                        var bytes = await _servicioTamanoDirectorio.CalcularAsync(par.RutaDestino, cancelacion);
                        par.TamanoDestinoBytes = bytes;
                        par.TamanoDestinoTexto = ServicioFormateoEstadisticas.FormatearBytes(bytes);
                    }
                }
                catch (OperationCanceledException)
                {
                    par.TamanoDestinoTexto = "—";
                    throw;
                }
                catch
                {
                    par.TamanoDestinoTexto = "No disponible";
                }
                finally
                {
                    par.TamanoDestinoCalculando = false;
                }

                if (!forzar && Pares.Count > 1)
                {
                    await Task.Delay(PausaEntreParesRecalculoAutomatico, cancelacion);
                }
            }

            if (!forzar)
            {
                _ultimoRecalculoTamanosAutomatico = DateTimeOffset.UtcNow;
            }
        }
        catch (OperationCanceledException)
        {
            // Nueva petición de cálculo reemplazó la anterior.
        }
        finally
        {
            CalculandoTamanosDestino = false;
        }
    }

    private void ProgramarRecalculoTamanosTrasSesion()
    {
        if (_temporizadorRecalculoTamanos is null)
        {
            _temporizadorRecalculoTamanos = App.DispatcherQueue.CreateTimer();
            _temporizadorRecalculoTamanos.Interval = RetardoRecalculoTrasSesion;
            _temporizadorRecalculoTamanos.Tick += async (_, _) =>
            {
                _temporizadorRecalculoTamanos.Stop();
                await ActualizarTamanosDestinoInternoAsync(forzar: false);
            };
        }

        _temporizadorRecalculoTamanos.Stop();
        _temporizadorRecalculoTamanos.Start();
    }

    /// <summary>Actualiza metadatos del panel oculto antes de capturarlo como imagen.</summary>
    public void PrepararExportacionParesActivos()
    {
        TextoExportacionPerfil = PerfilActual();
        TextoExportacionFecha = DateTime.Now.ToString("g");
        ActualizarColeccionParesFuncionando();
        ActualizarParesExportacionImagen();
    }

    private void ActualizarParesExportacionImagen()
    {
        ParesExportacionImagen.Clear();

        foreach (var monitor in MonitorParesFuncionando)
        {
            var config = BuscarParConfiguracion(string.Empty, monitor.Nombre);
            ParesExportacionImagen.Add(new ParExportacionImagenViewModel
            {
                Nombre = monitor.Nombre,
                RutaOrigen = config?.RutaOrigen ?? "—",
                RutaDestino = string.IsNullOrWhiteSpace(config?.RutaDestino)
                    ? monitor.RutaDestino
                    : config.RutaDestino,
                TamanoDestinoTexto = config?.TamanoDestinoTexto ?? monitor.TamanoDestinoTexto,
                Estado = monitor.Estado,
                Copiados = monitor.Copiados,
                Errores = monitor.Errores
            });
        }
    }

    private void ActualizarColeccionParesFuncionando()
    {
        MonitorParesFuncionando.Clear();
        foreach (var par in MonitorPares.Where(p => p.EstaFuncionando))
        {
            MonitorParesFuncionando.Add(par);
        }
    }

    private ParFilaViewModel? BuscarParConfiguracion(string idPar, string nombre) =>
        Pares.FirstOrDefault(p =>
            string.Equals(p.IdPar, idPar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Nombre, nombre, StringComparison.OrdinalIgnoreCase));

    private void LimpiarVistaTelemetria()
    {
        TextoResumen = "Demonio detenido — sin telemetría en vivo.";
        TextoPolling = "—";
        MonitorPares.Clear();
        MonitorParesFuncionando.Clear();
        CopiasEnCurso.Clear();
        ActividadReciente.Clear();
        MostrarEstadoVacioCopiasEnCurso = true;
        MostrarEstadoVacioActividadReciente = true;
        ActualizarPanelInicio(null);
    }

    private void ActualizarPanelInicio(EstadoPerfil? estado)
    {
        TextoDashboardPerfil = $"Perfil activo: {PerfilActual()}";

        var paresActivos = Pares.Where(p => p.Habilitado && !p.Pausado).ToList();
        if (paresActivos.Count == 0)
        {
            TextoDashboardOrigenDestino =
                "Todavía no hay pares activos. Crea uno en Sincronización para definir origen y destino.";
        }
        else if (paresActivos.Count == 1)
        {
            var unico = paresActivos[0];
            TextoDashboardOrigenDestino =
                $"Origen:\n{unico.RutaOrigen}\n\nDestino:\n{unico.RutaDestino}";
        }
        else
        {
            TextoDashboardOrigenDestino =
                $"{paresActivos.Count} pares activos. Abre Sincronización para ver cada origen → destino.";
        }

        if (estado is null || !DemonioEnEjecucion)
        {
            TextoDashboardProgreso = "—";
            TextoArchivoActual = "—";
            TextoErroresResumen = "—";
            TextoArchivosCopiadosSesion = "—";
            TextoColaHumana = "Pulsa Iniciar para comenzar la sincronización.";
            TextoUltimaSyncCorrecta = ResolverTextoUltimaSyncCorrecta();
            return;
        }

        TextoErroresResumen = estado.Totales.Errores == 0
            ? "0 errores en esta sesión"
            : $"{estado.Totales.Errores:N0} error(es) en esta sesión";

        TextoArchivosCopiadosSesion =
            $"{estado.Totales.Copiados:N0} archivos copiados ({ServicioFormateoEstadisticas.FormatearBytes(estado.Totales.BytesEscritos)})";

        TextoColaHumana = estado.ColaCopiaPendiente == 0
            ? "Cola vacía — no hay archivos pendientes"
            : $"{estado.ColaCopiaPendiente:N0} archivos pendientes en cola";

        var ultimaActividad = estado.Pares
            .Select(p => p.UltimaSincronizacion)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => DateTimeOffset.TryParse(s, out var instante) ? instante : (DateTimeOffset?)null)
            .Where(i => i.HasValue)
            .Select(i => i!.Value)
            .DefaultIfEmpty()
            .Max();

        TextoUltimaSyncCorrecta = ultimaActividad == default
            ? "Aún no hay sincronizaciones registradas en esta sesión"
            : $"Última actividad: {ServicioFormateoEstadisticas.FormatearInstanteUtc(ultimaActividad.ToString("o"))}";

        var copia = estado.CopiasEnCurso.FirstOrDefault();
        if (copia is not null)
        {
            TextoArchivoActual = copia.Archivo;
            var eta = copia.EtaSegundos is > 0
                ? $", restante ~{ServicioFormateoEstadisticas.FormatearDuracion(TimeSpan.FromSeconds(copia.EtaSegundos.Value))}"
                : string.Empty;
            TextoDashboardProgreso =
                $"{copia.Porcentaje}% ({ServicioFormateoEstadisticas.FormatearBytes(copia.BytesCopiados)} / {ServicioFormateoEstadisticas.FormatearBytes(copia.BytesTotales)}){eta}";
        }
        else if (estado.ColaCopiaPendiente > 0)
        {
            TextoArchivoActual = "Preparando siguiente archivo…";
            TextoDashboardProgreso = $"{estado.ColaCopiaPendiente:N0} en cola";
        }
        else
        {
            TextoArchivoActual = "En vigilancia — esperando cambios";
            TextoDashboardProgreso = "Sin copias activas";
        }

        TextoResumen = $"{TextoColaHumana}. {TextoArchivosCopiadosSesion}. {TextoErroresResumen}.";
    }

    private void ActualizarEstadoVacioRegistro() =>
        MostrarEstadoVacioRegistro = LineasRegistro.Count == 0;

    private string ResolverTextoUltimaSyncCorrecta()
    {
        var ultima = ServicioHistorialSesiones.ObtenerUltimaSesionCorrecta(PerfilActual());
        return ultima is null
            ? "Aún no hay una sesión completada sin errores"
            : $"Última sesión correcta: {ultima.TextoInicio} ({ultima.TextoResumen})";
    }

    private void CargarHistorialSesiones(string perfil)
    {
        HistorialSesiones.Clear();
        foreach (var entrada in ServicioHistorialSesiones.CargarRecientes(perfil))
        {
            HistorialSesiones.Add(entrada);
        }

        MostrarHistorialSesionesVacio = HistorialSesiones.Count == 0;
    }

    private void EvaluarTransicionSesion(string perfil, EstadoPerfil? estado, bool enEjecucion)
    {
        if (enEjecucion && !_demonioEstabaEnEjecucion)
        {
            var inicio = DateTimeOffset.UtcNow;
            if (estado is not null && DateTimeOffset.TryParse(estado.InicioSesionUtc, out var inicioIpc))
            {
                inicio = inicioIpc;
            }

            _datosSesionActual = new DatosSesionEnCurso { InicioUtc = inicio };
        }
        else if (enEjecucion && _datosSesionActual is not null && estado is not null)
        {
            _datosSesionActual.Copiados = estado.Totales.Copiados;
            _datosSesionActual.Errores = estado.Totales.Errores;
            _datosSesionActual.BytesEscritos = estado.Totales.BytesEscritos;
        }
        else if (!enEjecucion && _demonioEstabaEnEjecucion && _datosSesionActual is not null)
        {
            ServicioHistorialSesiones.RegistrarSesionFinalizada(
                perfil,
                _datosSesionActual.InicioUtc,
                DateTimeOffset.UtcNow,
                _datosSesionActual.Copiados,
                _datosSesionActual.Errores,
                _datosSesionActual.BytesEscritos);
            _datosSesionActual = null;
            CargarHistorialSesiones(perfil);
            ProgramarRecalculoTamanosTrasSesion();
        }

        _demonioEstabaEnEjecucion = enEjecucion;
    }

    private void ActualizarEstadoBotones()
    {
        var enEjecucion = _ipc.EstaDemonioEnEjecucion(PerfilActual());
        DemonioEnEjecucion = enEjecucion;
        PuedeIniciar = !enEjecucion;
        PuedeDetener = enEjecucion;
        PuedeEditarConfig = !enEjecucion;
        TextoEstado = enEjecucion ? "Sincronizando" : "Detenido";
        TextoEstadoAccesible = enEjecucion
            ? "Estado: Sincronizando — el demonio está copiando o vigilando archivos"
            : "Estado: Detenido — pulsa Iniciar para comenzar";
        ColorEstado = new SolidColorBrush(
            enEjecucion
                ? Windows.UI.Color.FromArgb(255, 27, 67, 50) // #1B4332 verde bosque sobrio
                : Windows.UI.Color.FromArgb(255, 155, 44, 44)); // #9B2C2C rojo burdeos sobrio
        OnPropertyChanged(nameof(ColorEstado));
        ActualizarBandejaSistema();
    }

    private Task RefrescarEstadoDemonioEnUiAsync()
    {
        var completado = new TaskCompletionSource();

        App.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                ActualizarEstadoBotones();
                completado.SetResult();
            }
            catch (Exception ex)
            {
                completado.SetException(ex);
            }
        });

        return completado.Task;
    }

    private async Task ActualizarVistaAsync()
    {
        var perfil = PerfilActual();
        var estado = await _ipc.LeerEstadoAsync(perfil).ConfigureAwait(false);

        App.DispatcherQueue.TryEnqueue(() =>
        {
            ActualizarEstadoBotones();
            EvaluarTransicionSesion(perfil, estado, DemonioEnEjecucion);

            if (estado is not null)
            {
                TextoPolling = estado.ProximoPollingEnSegundos.HasValue
                    ? $"Próximo barrido (par más próximo): en {estado.ProximoPollingEnSegundos}s"
                    : "Próximo barrido de seguridad: —";

                foreach (var parResumen in estado.Pares)
                {
                    var parViewModel = BuscarParConfiguracion(parResumen.IdPar, parResumen.Nombre);
                    if (parViewModel is not null)
                    {
                        parViewModel.TienePurgaMasivaBloqueada = parResumen.PurgaMasivaBloqueada;
                        parViewModel.CantidadArchivosPurgaBloqueada = parResumen.ArchivosPurgaBloqueados;
                    }
                }

                ServicioSincronizacionLista.SincronizarInPlace(
                    MonitorPares,
                    estado.Pares.Count,
                    (indice, fila) =>
                    {
                        var par = estado.Pares[indice];
                        var config = BuscarParConfiguracion(par.IdPar, par.Nombre);
                        var rutaDestino = config?.RutaDestino ?? string.Empty;
                        var tamano = config?.TamanoDestinoTexto ?? "—";
                        var funcionando = DemonioEnEjecucion
                            && string.Equals(par.Estado, "OK", StringComparison.OrdinalIgnoreCase);
                        fila.ActualizarDesde(
                            par.Nombre,
                            par.Estado,
                            rutaDestino,
                            tamano,
                            par.Copiados,
                            par.Errores,
                            funcionando);
                    },
                    indice =>
                    {
                        var par = estado.Pares[indice];
                        var config = BuscarParConfiguracion(par.IdPar, par.Nombre);
                        var rutaDestino = config?.RutaDestino ?? string.Empty;
                        var tamano = config?.TamanoDestinoTexto ?? "—";
                        var funcionando = DemonioEnEjecucion
                            && string.Equals(par.Estado, "OK", StringComparison.OrdinalIgnoreCase);
                        return MonitorParViewModel.Crear(
                            par.Nombre,
                            par.Estado,
                            rutaDestino,
                            tamano,
                            par.Copiados,
                            par.Errores,
                            funcionando);
                    });

                ActualizarColeccionParesFuncionando();

                ServicioSincronizacionLista.SincronizarInPlace(
                    CopiasEnCurso,
                    estado.CopiasEnCurso.Count,
                    (indice, fila) =>
                    {
                        var copia = estado.CopiasEnCurso[indice];
                        fila.ActualizarDesde(
                            copia.Copiador,
                            copia.Archivo,
                            copia.IdPar,
                            copia.Porcentaje,
                            copia.EtaSegundos,
                            copia.BytesTotales);
                    },
                    indice =>
                    {
                        var copia = estado.CopiasEnCurso[indice];
                        return CopiaEnCursoViewModel.Crear(
                            copia.Copiador,
                            copia.Archivo,
                            copia.IdPar,
                            copia.Porcentaje,
                            copia.EtaSegundos,
                            copia.BytesTotales);
                    });

                var mapaNombrePar = ConstruirMapaIdNombrePar();
                var actividad = estado.ActividadReciente
                    .Select(a => new ActividadViewModel
                    {
                        Hora = a.Hora,
                        Tipo = a.Tipo,
                        Archivo = a.Archivo,
                        IdPar = a.IdPar,
                        NombrePar = mapaNombrePar.TryGetValue(a.IdPar, out var nombre)
                            ? nombre
                            : a.IdPar
                    })
                    .ToList();

                ServicioSincronizacionLista.SincronizarHistorial(
                    ActividadReciente,
                    actividad,
                    static (a, b) =>
                        a.Hora == b.Hora
                        && a.Tipo == b.Tipo
                        && a.Archivo == b.Archivo
                        && a.IdPar == b.IdPar
                        && a.NombrePar == b.NombrePar);

                MostrarEstadoVacioCopiasEnCurso = CopiasEnCurso.Count == 0;
                MostrarEstadoVacioActividadReciente = ActividadReciente.Count == 0;
            }
            else
            {
                LimpiarVistaTelemetria();
            }

            ActualizarPanelInicio(estado);

            _monitorNotificaciones.Evaluar(new ContextoEvaluacionNotificaciones
            {
                NotificacionesHabilitadas = NotificacionesHabilitadas,
                Perfil = perfil,
                EnEjecucion = DemonioEnEjecucion,
                CopiadosSesion = estado?.Totales.Copiados ?? 0,
                ErroresSesion = estado?.Totales.Errores ?? 0,
                Pares = Pares
                    .Select(p => new SnapshotParNotificacion(
                        p.IdPar,
                        p.Nombre,
                        p.RutaOrigen,
                        p.RutaDestino,
                        p.Habilitado,
                        p.Pausado))
                    .ToList(),
                LineasRegistro = LineasRegistro.ToList()
            });

            ActualizarBandejaSistema();
            ActualizarRegistro(perfil);
            Estadisticas.ActualizarDesdeEstado(estado, ObtenerTamanoLogBytes(perfil), perfil);
        });
    }

    private static long ObtenerTamanoLogBytes(string perfil)
    {
        try
        {
            var ruta = RutasDatos.ResolverRutaLog(perfil);
            return File.Exists(ruta) ? new FileInfo(ruta).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private void ReiniciarRegistroPerfil()
    {
        _posicionLog = 0;
        _textoRegistroCrudo = string.Empty;
        _indiceLineaCrudaProcesada = 0;
        LineasRegistro.Clear();
        ActualizarFiltrosParRegistro();
        ActualizarEstadoVacioRegistro();
        ActualizarResumenRegistroFiltrado();
        ActualizarRegistro(PerfilActual(), moverAlFinal: true);
    }

    private void ActualizarFiltrosParRegistro()
    {
        var seleccionActual = ParFiltroRegistroSeleccionado;
        FiltrosParRegistro.Clear();
        FiltrosParRegistro.Add(ServicioFiltradoRegistro.EtiquetaTodosLosPares);

        foreach (var nombre in Pares
                     .Select(p => p.Nombre)
                     .Where(n => !string.IsNullOrWhiteSpace(n))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            FiltrosParRegistro.Add(nombre);
        }

        if (!FiltrosParRegistro.Contains(seleccionActual))
        {
            ParFiltroRegistroSeleccionado = ServicioFiltradoRegistro.EtiquetaTodosLosPares;
        }
    }

    private Dictionary<string, string> ConstruirMapaIdNombrePar() =>
        Pares.ToDictionary(p => p.IdPar, p => p.Nombre, StringComparer.OrdinalIgnoreCase);

    private void ReconstruirRegistroFiltrado(bool moverAlFinal)
    {
        LineasRegistro.Clear();
        _indiceLineaCrudaProcesada = 0;
        AnexarLineasRegistroVisibles(moverAlFinal);
        ActualizarEstadoVacioRegistro();
        ActualizarResumenRegistroFiltrado();
    }

    private void AnexarLineasRegistroVisibles(bool moverAlFinal)
    {
        var lineasCrudas = ServicioAnalisisRegistro.DividirLineasCrudas(_textoRegistroCrudo);
        if (_indiceLineaCrudaProcesada >= lineasCrudas.Length)
        {
            return;
        }

        var anexadas = ServicioAnalisisRegistro.AnexarLineasFiltradas(
            lineasCrudas,
            _indiceLineaCrudaProcesada,
            ParFiltroRegistroSeleccionado,
            NivelFiltroRegistroSeleccionado,
            TextoBusquedaRegistro,
            ConstruirMapaIdNombrePar(),
            LineasRegistro);

        _indiceLineaCrudaProcesada = lineasCrudas.Length;

        ActualizarEstadoVacioRegistro();
        ActualizarResumenRegistroFiltrado();

        if (moverAlFinal && anexadas > 0)
        {
            RegistroDesplazarAlFinalSolicitado?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ActualizarRegistro(string perfil, bool moverAlFinal = false)
    {
        var rutaLog = RutasDatos.ResolverRutaLog(perfil);
        if (!File.Exists(rutaLog))
        {
            ActualizarEstadoVacioRegistro();
            return;
        }

        try
        {
            using var flujo = new FileStream(rutaLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (flujo.Length < _posicionLog)
            {
                _posicionLog = 0;
                _textoRegistroCrudo = string.Empty;
                _indiceLineaCrudaProcesada = 0;
                LineasRegistro.Clear();
            }

            flujo.Seek(_posicionLog, SeekOrigin.Begin);
            using var lector = new StreamReader(flujo, Encoding.UTF8);
            var nuevo = lector.ReadToEnd();
            _posicionLog = flujo.Length;

            if (!string.IsNullOrEmpty(nuevo))
            {
                if (_textoRegistroCrudo.Length > 0 && !_textoRegistroCrudo.EndsWith('\n'))
                {
                    _textoRegistroCrudo += Environment.NewLine;
                }

                _textoRegistroCrudo += nuevo;
                if (_textoRegistroCrudo.Length > 200_000)
                {
                    _textoRegistroCrudo = _textoRegistroCrudo[^100_000..];
                    ReconstruirRegistroFiltrado(moverAlFinal: true);
                    return;
                }

                AnexarLineasRegistroVisibles(moverAlFinal: true);
            }
            else if (moverAlFinal && LineasRegistro.Count > 0)
            {
                RegistroDesplazarAlFinalSolicitado?.Invoke(this, EventArgs.Empty);
            }
        }
        catch
        {
            // No bloquear la UI por el log.
        }
    }
}
