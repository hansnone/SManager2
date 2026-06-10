using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SManager.Core.Modelos;
using SManager.Gui.Shared;
using SManager.Gui.WinUI.Models;
using SManager.Gui.Shared.Modelos;
using SManager.Gui.Shared.Servicios;
using SManager.Gui.WinUI.Servicios;
using SManager.Ipc;
using SManager.Ipc.Modelos;

namespace SManager.Gui.WinUI.ViewModels;

/// <summary>Estado y comandos del panel de control SManager 2.0.</summary>
public partial class MainPageViewModel : ObservableObject, IDisposable
{
    private readonly ServicioIpc _ipc = new();
    private readonly ServicioConfiguracionGui _servicioConfig = new();
    private readonly ControladorDaemon _daemon = new();
    private DispatcherQueueTimer? _temporizador;

    private ConfiguracionAplicacion _configuracion = ServicioConfiguracionGui.CrearPorDefecto();
    private string _rutaConfig = string.Empty;
    private long _posicionLog;
    private string _textoRegistroCrudo = string.Empty;
    private int _indiceLineaCrudaProcesada;
    private bool _suprimirMarcadoSucio;
    private bool _omitirProximoCambioPerfil;

    /// <summary>La vista pide desplazarse al final del registro (carga inicial o líneas nuevas).</summary>
    public event EventHandler? RegistroDesplazarAlFinalSolicitado;

    public ObservableCollection<string> Perfiles { get; } = [];
    public ObservableCollection<string> FiltrosParRegistro { get; } = [];
    public ObservableCollection<string> FiltrosNivelRegistro { get; } =
    [
        MapeadorNivelRegistro.EtiquetaTodosLosNiveles,
        "INFO",
        "WARN",
        "ERROR",
        "PENDIENTE"
    ];
    public ObservableCollection<ParFilaViewModel> Pares { get; } = [];
    public ObservableCollection<LineaRegistroViewModel> LineasRegistro { get; } = [];
    public ObservableCollection<MonitorParViewModel> MonitorPares { get; } = [];
    public ObservableCollection<CopiaEnCursoViewModel> CopiasEnCurso { get; } = [];
    public ObservableCollection<ActividadViewModel> ActividadReciente { get; } = [];

    public EstadisticasPanelViewModel Estadisticas { get; } = new();

    /// <summary>Apartados de la guía de referencia (sección Guía).</summary>
    public IReadOnlyList<SeccionGuiaViewModel> SeccionesGuia { get; } = ContenidoGuiaApp.ObtenerSecciones();

    [ObservableProperty]
    private string _perfilSeleccionado = "General";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoRutaConfiguracionBarra))]
    private string _rutaConfiguracion = string.Empty;

    /// <summary>True si la UI difiere del último guardado o carga desde disco.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoRutaConfiguracionBarra))]
    private bool _hayCambiosSinGuardar;

    /// <summary>Ruta JSON en la barra superior, con indicador visual si hay cambios pendientes.</summary>
    public string TextoRutaConfiguracionBarra =>
        HayCambiosSinGuardar
            ? $"{RutaConfiguracion}  •  sin guardar"
            : RutaConfiguracion;

    /// <summary>Ubicación estándar del perfil en Perfiles configuracion.</summary>
    [ObservableProperty]
    private string _rutaConfiguracionPorDefecto = string.Empty;

    /// <summary>True si el perfil usa un JSON fuera de la carpeta por defecto.</summary>
    [ObservableProperty]
    private bool _usaRutaConfigPersonalizada;

    [ObservableProperty]
    private string _textoModoRutaConfig = "Por defecto";

    [ObservableProperty]
    private string _textoEstado = "Detenido";

    /// <summary>Indicador visual junto al estado en la barra superior.</summary>
    public SolidColorBrush ColorEstado { get; private set; } = new(Microsoft.UI.Colors.IndianRed);

    [ObservableProperty]
    private string _textoResumen = string.Empty;

    [ObservableProperty]
    private string _textoPolling = string.Empty;

    [ObservableProperty]
    private string _textoDashboardPerfil = string.Empty;

    [ObservableProperty]
    private string _textoDashboardOrigenDestino = string.Empty;

    [ObservableProperty]
    private string _textoDashboardProgreso = "—";

    [ObservableProperty]
    private string _textoArchivoActual = "—";

    [ObservableProperty]
    private string _textoUltimaSyncCorrecta = "—";

    [ObservableProperty]
    private string _textoErroresResumen = "—";

    [ObservableProperty]
    private string _textoArchivosCopiadosSesion = "—";

    [ObservableProperty]
    private string _textoColaHumana = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeAnalizarCambios))]
    private bool _analizandoCambios;

    /// <summary>Habilita el botón Analizar cambios mientras no hay análisis en curso.</summary>
    public bool PuedeAnalizarCambios => !AnalizandoCambios;

    [ObservableProperty]
    private bool _mostrarEstadoVacioRegistro = true;

    [ObservableProperty]
    private bool _mostrarAsistenteOnboarding;

    [ObservableProperty]
    private bool _mostrarConsejosContextuales = true;

    [ObservableProperty]
    private bool _consejoIniciarAbierto;

    [ObservableProperty]
    private bool _consejoAnalizarAbierto;

    public AsistenteOnboardingViewModel Asistente { get; } = new();

    [ObservableProperty]
    private string _parFiltroRegistroSeleccionado = ServicioFiltradoRegistro.EtiquetaTodosLosPares;

    [ObservableProperty]
    private string _nivelFiltroRegistroSeleccionado = MapeadorNivelRegistro.EtiquetaTodosLosNiveles;

    [ObservableProperty]
    private string _textoBusquedaRegistro = string.Empty;

    [ObservableProperty]
    private string _textoResumenRegistroFiltrado = string.Empty;

    [ObservableProperty]
    private bool _minimizarABandejaAlCerrar = true;

    [ObservableProperty]
    private bool _notificacionesHabilitadas = true;

    [ObservableProperty]
    private bool _bandejaHabilitada = true;

    [ObservableProperty]
    private bool _modoInterfazAvanzado;

    [ObservableProperty]
    private string _temaAplicacion = ServicioTemaAplicacion.TemaSistema;

    [ObservableProperty]
    private string _idiomaUiEtiqueta = ServicioTextosUi.EtiquetaIdiomaEspanol;

    /// <summary>Texto completo del estado para lectores de pantalla (no depende solo del color).</summary>
    [ObservableProperty]
    private string _textoEstadoAccesible = "Estado: Detenido";

    [ObservableProperty]
    private bool _mostrarHistorialSesionesVacio = true;

    public ObservableCollection<HistorialSesionViewModel> HistorialSesiones { get; } = [];

    public IReadOnlyList<string> OpcionesTema => ServicioTemaAplicacion.Opciones;

    public IReadOnlyList<string> OpcionesIdioma => ServicioTextosUi.OpcionesIdiomaEtiqueta;

    /// <summary>La vista principal reacciona al cambiar modo básico/avanzado.</summary>
    public event EventHandler? ModoInterfazCambiado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    [NotifyCanExecuteChangedFor(nameof(NuevoPerfilCommand))]
    [NotifyCanExecuteChangedFor(nameof(EliminarPerfilCommand))]
    [NotifyCanExecuteChangedFor(nameof(AnadirParCommand))]
    [NotifyCanExecuteChangedFor(nameof(QuitarParCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditarParCommand))]
    private bool _puedeEditarConfig = true;

    /// <summary>Habilita Iniciar en XAML sin depender de CanExecute del RelayCommand (WinUI lo pisa).</summary>
    [ObservableProperty]
    private bool _puedeIniciar = true;

    /// <summary>Habilita Detener/Recargar en XAML.</summary>
    [ObservableProperty]
    private bool _puedeDetener;

    [ObservableProperty]
    private bool _demonioEnEjecucion;

    [ObservableProperty]
    private ParFilaViewModel? _parSeleccionado;

    /// <summary>True cuando no hay pares: la vista muestra estado vacío con CTA.</summary>
    [ObservableProperty]
    private bool _mostrarEstadoVacioPares = true;

    [ObservableProperty]
    private int _intervaloPollingSegundos = 180;

    [ObservableProperty]
    private int _segundosEstabilidadArchivo = 3;

    [ObservableProperty]
    private int _numCopiadoresParalelos = 4;

    [ObservableProperty]
    private int _numHidratadoresParalelos = 3;

    [ObservableProperty]
    private int _timeoutHidratacionSegundos = 300;

    [ObservableProperty]
    private int _intervaloPublicacionEstadoMs = 500;

    [ObservableProperty]
    private bool _autoInicioHabilitado;

    [ObservableProperty]
    private bool _autoInicioMinimizado = true;

    private bool _cargandoPreferenciasAutoInicio;

    private ServicioBandejaSistema? _bandeja;

    private readonly ServicioMonitorNotificaciones _monitorNotificaciones = new();

    private bool _cargandoPreferenciasGui;

    private bool _salirExplicitoSolicitado;

    private bool _demonioEstabaEnEjecucion;

    private DatosSesionEnCurso? _datosSesionActual;

    /// <summary>Contadores de la sesión en curso mientras el demonio está activo.</summary>
    private sealed class DatosSesionEnCurso
    {
        public DateTimeOffset InicioUtc { get; init; }

        public int Copiados { get; set; }

        public int Errores { get; set; }

        public long BytesEscritos { get; set; }
    }

    public MainPageViewModel()
    {
        Pares.CollectionChanged += Pares_CollectionChanged;
        Asistente.FinalizadoSolicitado += ProcesarAsistenteFinalizadoAsync;
        Asistente.CerrarSolicitado += (_, _) => CerrarAsistenteOnboarding();
    }

    private void Pares_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        ActualizarEstadoVacioPares();

        if (e.NewItems is not null)
        {
            foreach (ParFilaViewModel par in e.NewItems)
            {
                SuscribirCambiosPar(par);
                par.ActualizarAvisosRiesgo();
            }
        }

        if (e.OldItems is not null)
        {
            foreach (ParFilaViewModel par in e.OldItems)
            {
                DesuscribirCambiosPar(par);
            }
        }

        MarcarComoSucio();
    }

    private void SuscribirCambiosPar(ParFilaViewModel par) =>
        par.PropertyChanged += Par_PropertyChanged;

    private void DesuscribirCambiosPar(ParFilaViewModel par) =>
        par.PropertyChanged -= Par_PropertyChanged;

    private void Par_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        MarcarComoSucio();

    public void Inicializar()
    {
        // El temporizador depende del hilo UI; se crea aquí, no en el ctor (MainPage se construye muy pronto).
        if (_temporizador is null)
        {
            _temporizador = App.DispatcherQueue.CreateTimer();
            _temporizador.Interval = TimeSpan.FromMilliseconds(500);
            _temporizador.Tick += async (_, _) => await ActualizarVistaAsync();
        }

        CargarListaPerfiles();
        CargarConfiguracionPerfilActual();
        CargarPreferenciasAutoInicio();
        CargarPreferenciasGui();
        InicializarBandejaYNotificaciones();
        CargarHistorialSesiones(PerfilActual());
        ServicioTemaAplicacion.Aplicar(TemaAplicacion);
        ActualizarEstadoBotones();
        ReiniciarRegistroPerfil();
        _temporizador.Start();
        _omitirProximoCambioPerfil = true;

        if (ServicioOnboarding.DebeMostrarAsistenteAutomatico(Pares.Count > 0))
        {
            MostrarAsistenteOnboarding = true;
        }
        else
        {
            IntentarMostrarConsejosIniciales();
        }
    }

    private void CargarPreferenciasGui()
    {
        _cargandoPreferenciasGui = true;
        try
        {
            var preferencias = ServicioPreferenciasGui.Cargar();
            MostrarConsejosContextuales = preferencias.MostrarConsejosContextuales;
            MinimizarABandejaAlCerrar = preferencias.MinimizarABandejaAlCerrar;
            NotificacionesHabilitadas = preferencias.NotificacionesHabilitadas;
            BandejaHabilitada = preferencias.BandejaHabilitada;
            ModoInterfazAvanzado = preferencias.ModoInterfazAvanzado;
            TemaAplicacion = ServicioTemaAplicacion.Normalizar(preferencias.TemaAplicacion);
            IdiomaUiEtiqueta = ServicioTextosUi.EtiquetaDesdeCodigo(preferencias.IdiomaUi);
        }
        finally
        {
            _cargandoPreferenciasGui = false;
        }
    }

    private void InicializarBandejaYNotificaciones()
    {
        ServicioNotificacionesWindows.Inicializar();

        if (!BandejaHabilitada)
        {
            return;
        }

        try
        {
            var rutaIcono = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            _bandeja = new ServicioBandejaSistema();
            _bandeja.Inicializar(rutaIcono);
            ActualizarBandejaSistema();
        }
        catch
        {
            _bandeja?.Dispose();
            _bandeja = null;
        }
    }

    private void ActualizarBandejaSistema()
    {
        if (_bandeja is null || !BandejaHabilitada)
        {
            return;
        }

        var tooltip = DemonioEnEjecucion
            ? $"SManager — Sincronizando ({PerfilActual()})"
            : $"SManager — Detenido ({PerfilActual()})";

        _bandeja.ActualizarEstado(tooltip, PuedeIniciar, PuedeDetener);
    }

    /// <summary>True si el cierre debe convertirse en ocultar a la bandeja.</summary>
    public bool DebeOcultarEnBandejaAlCerrar() =>
        BandejaHabilitada && MinimizarABandejaAlCerrar && !_salirExplicitoSolicitado;

    public void NotificarOcultadoEnBandeja()
    {
        if (NotificacionesHabilitadas)
        {
            ServicioNotificacionesWindows.Mostrar(
                "SManager sigue activo",
                "La aplicación continúa en la bandeja del sistema. Doble clic en el icono para abrirla.");
        }
    }

    public void SolicitarSalirAplicacion() => _salirExplicitoSolicitado = true;

    partial void OnMinimizarABandejaAlCerrarChanged(bool value) => PersistirPreferenciasGui();

    partial void OnNotificacionesHabilitadasChanged(bool value) => PersistirPreferenciasGui();

    partial void OnModoInterfazAvanzadoChanged(bool value)
    {
        PersistirPreferenciasGui();
        if (!_cargandoPreferenciasGui)
        {
            ModoInterfazCambiado?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnTemaAplicacionChanged(string value)
    {
        PersistirPreferenciasGui();
        if (!_cargandoPreferenciasGui)
        {
            ServicioTemaAplicacion.Aplicar(value);
        }
    }

    partial void OnIdiomaUiEtiquetaChanged(string value) => PersistirPreferenciasGui();

    partial void OnBandejaHabilitadaChanged(bool value)
    {
        PersistirPreferenciasGui();
        if (_cargandoPreferenciasGui)
        {
            return;
        }

        if (value && _bandeja is null)
        {
            InicializarBandejaYNotificaciones();
        }
        else if (!value)
        {
            _bandeja?.Dispose();
            _bandeja = null;
        }
    }

    private void PersistirPreferenciasGui()
    {
        if (_cargandoPreferenciasGui)
        {
            return;
        }

        var preferencias = ServicioPreferenciasGui.Cargar();
        preferencias.MinimizarABandejaAlCerrar = MinimizarABandejaAlCerrar;
        preferencias.NotificacionesHabilitadas = NotificacionesHabilitadas;
        preferencias.BandejaHabilitada = BandejaHabilitada;
        preferencias.ModoInterfazAvanzado = ModoInterfazAvanzado;
        preferencias.TemaAplicacion = ServicioTemaAplicacion.Normalizar(TemaAplicacion);
        preferencias.IdiomaUi = ServicioTextosUi.CodigoDesdeEtiqueta(IdiomaUiEtiqueta);
        ServicioPreferenciasGui.Guardar(preferencias);
    }

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
        await MostrarAvisoAsync($"Registro exportado a:\n{ruta}", "Exportación completada");
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

        await MostrarAvisoAsync($"Diagnóstico exportado a:\n{ruta}", "Exportación completada");
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

    /// <summary>Muestra consejos contextuales la primera vez que el usuario usa la app.</summary>
    public void IntentarMostrarConsejosIniciales()
    {
        if (!MostrarConsejosContextuales)
        {
            return;
        }

        if (ServicioOnboarding.DebeMostrarConsejo(ServicioOnboarding.ConsejoIniciar))
        {
            ConsejoIniciarAbierto = true;
        }
    }

    public void MarcarConsejoIniciarCerrado()
    {
        ConsejoIniciarAbierto = false;
        ServicioOnboarding.MarcarConsejoVisto(ServicioOnboarding.ConsejoIniciar);
    }

    public void MarcarConsejoAnalizarCerrado()
    {
        ConsejoAnalizarAbierto = false;
        ServicioOnboarding.MarcarConsejoVisto(ServicioOnboarding.ConsejoAnalizar);
    }

    partial void OnMostrarConsejosContextualesChanged(bool value)
    {
        var preferencias = ServicioPreferenciasGui.Cargar();
        preferencias.MostrarConsejosContextuales = value;
        ServicioPreferenciasGui.Guardar(preferencias);

        if (!value)
        {
            ConsejoIniciarAbierto = false;
            ConsejoAnalizarAbierto = false;
        }
    }

    [RelayCommand]
    private void AbrirAsistenteOnboarding()
    {
        Asistente.PasoActual = PasoAsistenteOnboarding.Bienvenida;
        MostrarAsistenteOnboarding = true;
    }

    private void CerrarAsistenteOnboarding() => MostrarAsistenteOnboarding = false;

    private async Task ProcesarAsistenteFinalizadoAsync(ResultadoAsistenteOnboarding resultado)
    {
        Pares.Add(resultado.Par);
        ParSeleccionado = resultado.Par;
        ActualizarFiltrosParRegistro();

        if (resultado.GuardarConfiguracion && PuedeEditarConfig)
        {
            GuardarSilencioso();
        }

        if (resultado.MarcarAsistenteCompletado)
        {
            ServicioOnboarding.MarcarAsistenteCompletado();
        }

        CerrarAsistenteOnboarding();

        if (resultado.IniciarSincronizacion && PuedeIniciar)
        {
            await IniciarAsync();
        }
        else
        {
            IntentarMostrarConsejosIniciales();
        }
    }

    /// <summary>Evita recargar el perfil dos veces cuando el ComboBox dispara SelectionChanged al arrancar.</summary>
    public bool ConsumirOmitirCambioPerfilInicial()
    {
        if (!_omitirProximoCambioPerfil)
        {
            return false;
        }

        _omitirProximoCambioPerfil = false;
        return true;
    }

    private void ActualizarEstadoVacioPares() =>
        MostrarEstadoVacioPares = Pares.Count == 0;

    partial void OnParFiltroRegistroSeleccionadoChanged(string value) =>
        ReconstruirRegistroFiltrado(moverAlFinal: false);

    partial void OnNivelFiltroRegistroSeleccionadoChanged(string value) =>
        ReconstruirRegistroFiltrado(moverAlFinal: false);

    partial void OnTextoBusquedaRegistroChanged(string value) =>
        ReconstruirRegistroFiltrado(moverAlFinal: false);

    partial void OnIntervaloPollingSegundosChanged(int value) => MarcarComoSucio();

    partial void OnSegundosEstabilidadArchivoChanged(int value) => MarcarComoSucio();

    partial void OnNumCopiadoresParalelosChanged(int value) => MarcarComoSucio();

    partial void OnNumHidratadoresParalelosChanged(int value) => MarcarComoSucio();

    partial void OnTimeoutHidratacionSegundosChanged(int value) => MarcarComoSucio();

    partial void OnIntervaloPublicacionEstadoMsChanged(int value) => MarcarComoSucio();

    partial void OnAutoInicioHabilitadoChanged(bool value)
    {
        if (_cargandoPreferenciasAutoInicio)
        {
            return;
        }

        PersistirPreferenciasAutoInicio();
    }

    partial void OnAutoInicioMinimizadoChanged(bool value)
    {
        if (_cargandoPreferenciasAutoInicio)
        {
            return;
        }

        PersistirPreferenciasAutoInicio();
    }

    private void CargarPreferenciasAutoInicio()
    {
        _cargandoPreferenciasAutoInicio = true;
        try
        {
            var preferencias = ServicioPreferenciasGui.Cargar();
            var registro = ServicioAutoInicioSistema.LeerEstado();
            var archivoExiste = File.Exists(
                Path.Combine(RutasDatos.ResolverRaiz(), "preferencias_gui.json"));

            if (!archivoExiste && registro.Habilitado)
            {
                // Instalador marcó autostart: reflejar en la GUI sin fichero previo.
                AutoInicioHabilitado = true;
                AutoInicioMinimizado = registro.Minimizado;
                ServicioPreferenciasGui.Guardar(new PreferenciasGuiDto
                {
                    AutoInicioHabilitado = AutoInicioHabilitado,
                    AutoInicioMinimizado = AutoInicioMinimizado
                });
                return;
            }

            AutoInicioHabilitado = preferencias.AutoInicioHabilitado;
            AutoInicioMinimizado = preferencias.AutoInicioMinimizado;

            if (AutoInicioHabilitado != registro.Habilitado
                || (AutoInicioHabilitado && AutoInicioMinimizado != registro.Minimizado))
            {
                ServicioAutoInicioSistema.Aplicar(AutoInicioHabilitado, AutoInicioMinimizado);
            }
        }
        finally
        {
            _cargandoPreferenciasAutoInicio = false;
        }
    }

    private void PersistirPreferenciasAutoInicio()
    {
        ServicioPreferenciasGui.Guardar(new PreferenciasGuiDto
        {
            AutoInicioHabilitado = AutoInicioHabilitado,
            AutoInicioMinimizado = AutoInicioMinimizado
        });

        try
        {
            ServicioAutoInicioSistema.Aplicar(AutoInicioHabilitado, AutoInicioMinimizado);
        }
        catch (Exception ex)
        {
            TextoEstado = $"No se pudo actualizar el auto-arranque: {ex.Message}";
        }
    }

    /// <summary>Carga el perfil ya seleccionado en el ComboBox (sin leer la UI del perfil anterior).</summary>
    [RelayCommand]
    private void AplicarCambioPerfil()
    {
        CargarConfiguracionPerfilActual();
        ActualizarEstadoBotones();
        _monitorNotificaciones.Reiniciar();
        _demonioEstabaEnEjecucion = DemonioEnEjecucion;
        _datosSesionActual = null;
        ReiniciarRegistroPerfil();
        Estadisticas.ReiniciarMuestreo();
        LimpiarVistaTelemetria();
        CargarHistorialSesiones(PerfilActual());
    }

    /// <summary>
    /// Pregunta qué hacer con cambios sin guardar antes de cambiar de perfil o cerrar la app.
    /// </summary>
    public async Task<DecisionCambiosPendientes> PreguntarCambiosSinGuardarAsync(string motivo)
    {
        if (!HayCambiosSinGuardar || !PuedeEditarConfig)
        {
            return DecisionCambiosPendientes.ContinuarSinGuardar;
        }

        var cuadro = new ContentDialog
        {
            Title = "Cambios sin guardar",
            Content = new TextBlock
            {
                Text =
                    $"Hay cambios en el perfil «{PerfilActual()}» que no se han guardado en disco.\n\n"
                    + $"¿Qué deseas hacer al {motivo}?",
                TextWrapping = TextWrapping.WrapWholeWords
            },
            PrimaryButtonText = "Guardar",
            SecondaryButtonText = "Descartar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.Window.Content.XamlRoot
        };

        var resultado = await cuadro.ShowAsync();
        return resultado switch
        {
            ContentDialogResult.Primary => DecisionCambiosPendientes.GuardarYContinuar,
            ContentDialogResult.Secondary => DecisionCambiosPendientes.ContinuarSinGuardar,
            _ => DecisionCambiosPendientes.Cancelar
        };
    }

    private void MarcarComoSucio()
    {
        if (_suprimirMarcadoSucio || !PuedeEditarConfig)
        {
            return;
        }

        HayCambiosSinGuardar = true;
    }

    private void MarcarComoGuardado()
    {
        _suprimirMarcadoSucio = true;
        HayCambiosSinGuardar = false;
        _suprimirMarcadoSucio = false;
    }

    /// <summary>Indica que los cambios en memoria se abandonan (p. ej. al cambiar de perfil sin guardar).</summary>
    public void DescartarCambiosPendientes() => MarcarComoGuardado();

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private async Task EliminarPerfilAsync()
    {
        var perfil = PerfilActual();
        if (_ipc.EstaDemonioEnEjecucion(perfil))
        {
            await MostrarAvisoAsync("Detén el demonio antes de eliminar este perfil.", "Demonio activo");
            return;
        }

        // Evita que el temporizador de la UI recree carpetas IPC mientras confirmamos o borramos.
        _temporizador?.Stop();

        var usaPersonalizada = _servicioConfig.UsaRutaPersonalizada(perfil);
        var rutaPersonalizada = usaPersonalizada
            ? _servicioConfig.ResolverRutaConfiguracion(perfil)
            : null;

        var casillaBorrarJson = new CheckBox
        {
            Content = "También borrar el archivo JSON personalizado del disco",
            IsEnabled = usaPersonalizada,
            IsChecked = false,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var textoAviso = usaPersonalizada
            ? $"Se eliminará el perfil «{perfil}» y sus datos en %LOCALAPPDATA%\\SManager2.\n\n"
              + $"Por defecto se conserva el JSON en:\n{rutaPersonalizada}"
            : $"Se eliminará el perfil «{perfil}» y sus datos en %LOCALAPPDATA%\\SManager2 "
              + "(configuración, log, telemetría IPC).\n\n"
              + "No se borran archivos ya copiados en destino.";

        var cuadro = new ContentDialog
        {
            Title = "Eliminar perfil",
            PrimaryButtonText = "Eliminar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.Window.Content.XamlRoot,
            Content = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = textoAviso,
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    casillaBorrarJson
                }
            }
        };

        if (await cuadro.ShowAsync() != ContentDialogResult.Primary)
        {
            _temporizador?.Start();
            return;
        }

        var resultado = ServicioEliminacionPerfil.Eliminar(
            perfil,
            _ipc,
            eliminarJsonPersonalizado: casillaBorrarJson.IsChecked == true);

        if (!resultado.Exito)
        {
            _temporizador?.Start();
            await MostrarAvisoAsync(resultado.MensajeError ?? "No se pudo eliminar el perfil.", "Error");
            return;
        }

        var resumen = resultado.ElementosEliminados.Count > 0
            ? "Eliminado:\n• " + string.Join("\n• ", resultado.ElementosEliminados)
            : "El perfil no tenía datos locales.";

        if (resultado.Advertencias.Count > 0)
        {
            resumen += "\n\n" + string.Join("\n", resultado.Advertencias);
        }

        // Recargar lista sin el perfil borrado y luego cambiar selección.
        var restantes = _servicioConfig.ListarPerfiles()
            .Where(p => !p.Equals(perfil, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        CargarListaPerfiles(excluirPerfil: perfil);
        PerfilSeleccionado = Perfiles.FirstOrDefault()
            ?? restantes.FirstOrDefault()
            ?? "General";
        if (!Perfiles.Contains(PerfilSeleccionado))
        {
            Perfiles.Add(PerfilSeleccionado);
        }

        CargarConfiguracionPerfilActual();
        ReiniciarRegistroPerfil();
        Estadisticas.ReiniciarPresentacion();
        LimpiarVistaTelemetria();
        ActualizarEstadoBotones();
        _temporizador?.Start();

        await MostrarAvisoAsync(resumen, "Perfil eliminado");
    }

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private async Task NuevoPerfilAsync()
    {
        var decision = await PreguntarCambiosSinGuardarAsync("crear otro perfil");
        if (decision == DecisionCambiosPendientes.Cancelar)
        {
            return;
        }

        if (decision == DecisionCambiosPendientes.GuardarYContinuar)
        {
            await GuardarAsync();
        }
        else if (decision == DecisionCambiosPendientes.ContinuarSinGuardar)
        {
            DescartarCambiosPendientes();
        }

        var cuadro = new ContentDialog
        {
            Title = "Nuevo perfil",
            PrimaryButtonText = "Crear",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.Window.Content.XamlRoot
        };

        var caja = new TextBox
        {
            PlaceholderText = "Nombre del perfil",
            Margin = new Thickness(0, 8, 0, 0)
        };
        cuadro.Content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = $"Se creará la configuración por defecto en:\n%LOCALAPPDATA%\\SManager2\\Perfiles configuracion\\<nombre>\\configuracion.json",
                    TextWrapping = TextWrapping.WrapWholeWords
                },
                caja
            }
        };

        var resultado = await cuadro.ShowAsync();
        if (resultado != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            ServicioConfiguracionGui.ValidarNombrePerfil(caja.Text.Trim());
            _servicioConfig.CrearPerfil(caja.Text.Trim());
            PerfilSeleccionado = caja.Text.Trim();
            CargarListaPerfiles();
            CargarConfiguracionPerfilActual();
        }
        catch (Exception ex)
        {
            await MostrarAvisoAsync(ex.Message, "No se pudo crear el perfil");
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private async Task GuardarAsync()
    {
        GuardarSilencioso();
        await MostrarAvisoAsync($"Configuración guardada en:\n{_rutaConfig}", "SManager 2.0");
    }

    /// <summary>Guarda sin diálogo de confirmación (asistente, flujos automáticos).</summary>
    private void GuardarSilencioso()
    {
        LeerConfiguracionDesdeUi();
        _servicioConfig.Guardar(_rutaConfig, _configuracion);
        MarcarComoGuardado();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task IniciarAsync()
    {
        if (!PuedeIniciar)
        {
            return;
        }

        try
        {
            LeerConfiguracionDesdeUi();
            _servicioConfig.Guardar(_rutaConfig, _configuracion);
            MarcarComoGuardado();

            if (!Pares.Any(p => p.Habilitado))
            {
                await MostrarAvisoAsync(
                    "Añade al menos un par activo (habilitado) antes de iniciar el demonio.",
                    "Sin pares");
                return;
            }

            var errores = EvaluarRutas(out var ok);
            if (!ok)
            {
                await MostrarAvisoAsync(string.Join('\n', errores), "Rutas no válidas");
                return;
            }

            // La CLI resuelve la ruta (por defecto o personalizada) si no se pasa -ConfigPath.
            var (codigo, salida, error) = await _daemon.EjecutarAsync(
                $"start -perfil \"{PerfilActual()}\"");

            if (codigo != 0)
            {
                await MostrarAvisoAsync(string.IsNullOrWhiteSpace(error) ? salida : error, "Error al iniciar");
            }

            _posicionLog = 0;
            _textoRegistroCrudo = string.Empty;
            LineasRegistro.Clear();
            CargarListaPerfiles();
        }
        finally
        {
            await RefrescarEstadoDemonioEnUiAsync();
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task DetenerAsync()
    {
        if (!PuedeDetener)
        {
            return;
        }

        PuedeDetener = false;
        TextoEstado = "Deteniendo…";

        try
        {
            var (codigo, salida, error) = await _daemon.EjecutarAsync($"stop -perfil \"{PerfilActual()}\"");
            if (codigo != 0)
            {
                await MostrarAvisoAsync(string.IsNullOrWhiteSpace(error) ? salida : error, "Error al detener");
            }
        }
        finally
        {
            await RefrescarEstadoDemonioEnUiAsync();
            await EsperarLiberacionPidAsync();
        }
    }

    /// <summary>Tras stop, el CLI puede tardar hasta ~90 s antes del cierre forzado.</summary>
    private async Task EsperarLiberacionPidAsync()
    {
        for (var i = 0; i < 200; i++)
        {
            if (!_ipc.EstaDemonioEnEjecucion(PerfilActual()))
            {
                await RefrescarEstadoDemonioEnUiAsync();
                return;
            }

            await Task.Delay(250);
        }

        await RefrescarEstadoDemonioEnUiAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RecargarAsync()
    {
        if (!PuedeDetener)
        {
            return;
        }

        var (codigo, salida, error) = await _daemon.EjecutarAsync($"reload -perfil \"{PerfilActual()}\"");
        if (codigo != 0)
        {
            await MostrarAvisoAsync(string.IsNullOrWhiteSpace(error) ? salida : error, "Error al recargar");
            return;
        }

        CargarConfiguracionPerfilActual();
    }

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private async Task AnadirParAsync()
    {
        var nuevo = await ServicioDialogoPar.MostrarAsync(null, esEdicion: false);
        if (nuevo is null)
        {
            return;
        }

        Pares.Add(nuevo);
        ParSeleccionado = nuevo;
        ActualizarFiltrosParRegistro();
    }

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private async Task EditarParAsync(ParFilaViewModel? par)
    {
        if (par is null)
        {
            return;
        }

        var editado = await ServicioDialogoPar.MostrarAsync(par, esEdicion: true);
        if (editado is null)
        {
            return;
        }

        CopiarDatosPar(editado, par);
        ActualizarFiltrosParRegistro();
    }

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private async Task QuitarParAsync(ParFilaViewModel? par)
    {
        var objetivo = par ?? ParSeleccionado;
        if (objetivo is null)
        {
            return;
        }

        var etiquetaPar = string.IsNullOrWhiteSpace(objetivo.Nombre)
            ? "el par seleccionado"
            : $"«{objetivo.Nombre.Trim()}»";

        var cuadro = new ContentDialog
        {
            Title = "Quitar par",
            Content = new TextBlock
            {
                Text = $"¿Eliminar {etiquetaPar} de la configuración?\n\nDebes pulsar Guardar para persistir el cambio en disco.",
                TextWrapping = TextWrapping.WrapWholeWords
            },
            PrimaryButtonText = "Eliminar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.Window.Content.XamlRoot
        };

        if (await cuadro.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        Pares.Remove(objetivo);
        if (ParSeleccionado == objetivo)
        {
            ParSeleccionado = null;
        }
        ActualizarFiltrosParRegistro();
    }

    private static void CopiarDatosPar(ParFilaViewModel origen, ParFilaViewModel destino)
    {
        destino.Nombre = origen.Nombre;
        destino.RutaOrigen = origen.RutaOrigen;
        destino.RutaDestino = origen.RutaDestino;
        destino.FiltroInclusion = origen.FiltroInclusion;
        destino.FiltroExclusion = origen.FiltroExclusion;
        destino.Habilitado = origen.Habilitado;
        destino.Pausado = origen.Pausado;
    }

    [RelayCommand]
    private async Task ValidarRutasAsync()
    {
        var errores = EvaluarRutas(out var ok);
        await MostrarAvisoAsync(
            ok ? "Todas las rutas existen." : string.Join('\n', errores),
            "Validación");
    }

    /// <summary>Simula la sincronización: cuenta archivos nuevos/modificados sin copiar nada.</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task AnalizarCambiosAsync()
    {
        if (AnalizandoCambios)
        {
            return;
        }

        LeerConfiguracionDesdeUi();
        var paresActivos = _configuracion.Pares
            .Where(p => p.Habilitado && !p.Pausado)
            .ToList();

        if (paresActivos.Count == 0)
        {
            await MostrarAvisoAsync(
                "Añade al menos un par activo (habilitado y no pausado) para analizar cambios.",
                "Sin pares activos");
            return;
        }

        AnalizandoCambios = true;
        try
        {
            var resultado = await Task.Run(() =>
                ServicioAnalisisCambios.AnalizarPares(paresActivos, CancellationToken.None))
                .ConfigureAwait(true);

            await MostrarDialogoAnalisisAsync(resultado).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await MostrarAvisoAsync(ex.Message, "No se pudo analizar").ConfigureAwait(true);
        }
        finally
        {
            AnalizandoCambios = false;
        }
    }

    private async Task MostrarDialogoAnalisisAsync(ResultadoAnalisisGlobal resultado)
    {
        var contenido = new StackPanel { Spacing = 12, MaxWidth = 520 };

        contenido.Children.Add(new TextBlock
        {
            Text = "Vista previa — no se ha copiado ningún archivo.",
            TextWrapping = TextWrapping.WrapWholeWords
        });

        var tabla = new Grid { ColumnSpacing = 12, RowSpacing = 6 };
        tabla.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        tabla.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        tabla.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        AgregarFilaAnalisis(tabla, 0, "Acción", "Cantidad", "Tamaño", negrita: true);
        AgregarFilaAnalisis(
            tabla,
            1,
            "Archivos nuevos",
            resultado.TotalNuevos.ToString("N0"),
            ServicioFormateoEstadisticas.FormatearBytes(resultado.TotalBytesNuevos));
        AgregarFilaAnalisis(
            tabla,
            2,
            "Archivos modificados",
            resultado.TotalModificados.ToString("N0"),
            ServicioFormateoEstadisticas.FormatearBytes(resultado.TotalBytesModificados));
        AgregarFilaAnalisis(
            tabla,
            3,
            "Omitidos por filtro",
            resultado.TotalOmitidos.ToString("N0"),
            ServicioFormateoEstadisticas.FormatearBytes(resultado.PorPar.Sum(p => p.BytesOmitidosFiltro)));
        AgregarFilaAnalisis(
            tabla,
            4,
            "Errores de acceso",
            resultado.TotalErrores.ToString("N0"),
            "—");
        AgregarFilaAnalisis(
            tabla,
            5,
            "Total pendiente (estimado)",
            (resultado.TotalNuevos + resultado.TotalModificados).ToString("N0"),
            ServicioFormateoEstadisticas.FormatearBytes(resultado.TotalBytesPendientes));

        contenido.Children.Add(tabla);

        foreach (var par in resultado.PorPar.Where(p => p.AvisosRiesgo.Count > 0))
        {
            foreach (var aviso in par.AvisosRiesgo)
            {
                contenido.Children.Add(new InfoBar
                {
                    Title = par.NombrePar,
                    Message = aviso,
                    Severity = InfoBarSeverity.Warning,
                    IsOpen = true,
                    IsClosable = false
                });
            }
        }

        var cuadro = new ContentDialog
        {
            Title = "Análisis de cambios",
            Content = contenido,
            CloseButtonText = "Cerrar",
            PrimaryButtonText = "Ir a sincronización",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.Window.Content.XamlRoot
        };

        if (await cuadro.ShowAsync() == ContentDialogResult.Primary)
        {
            // La vista cambia de sección desde code-behind si hace falta; aquí solo avisamos.
            await MostrarAvisoAsync("Usa el menú lateral «Sincronización» para revisar los pares.", "Sincronización");
        }
    }

    private static void AgregarFilaAnalisis(
        Grid tabla,
        int fila,
        string accion,
        string cantidad,
        string tamano,
        bool negrita = false)
    {
        while (tabla.RowDefinitions.Count <= fila)
        {
            tabla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var negritaFila = negrita;
        tabla.Children.Add(CrearCeldaAnalisis(accion, fila, 0, negritaFila));
        tabla.Children.Add(CrearCeldaAnalisis(cantidad, fila, 1, negritaFila));
        tabla.Children.Add(CrearCeldaAnalisis(tamano, fila, 2, negritaFila));
    }

    private static TextBlock CrearCeldaAnalisis(string texto, int fila, int columna, bool negrita)
    {
        var bloque = new TextBlock
        {
            Text = texto,
            FontWeight = negrita ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(bloque, fila);
        Grid.SetColumn(bloque, columna);
        return bloque;
    }

    /// <summary>
    /// Vacía log, telemetría IPC y paneles de registro/monitor/estadísticas del perfil activo.
    /// Requiere demonio detenido para no competir con el escritor de log.
    /// </summary>
    [RelayCommand]
    private async Task LimpiarDatosPerfilAsync()
    {
        var perfil = PerfilActual();
        if (_ipc.EstaDemonioEnEjecucion(perfil))
        {
            await MostrarAvisoAsync(
                "Detén el demonio antes de limpiar.\n\nCon el servicio en marcha el log sigue abierto y la telemetría se regenera al instante.",
                "Demonio activo");
            return;
        }

        var cuadro = new ContentDialog
        {
            Title = "Limpiar datos del perfil",
            Content = new TextBlock
            {
                Text =
                    $"Se vaciarán los datos derivados del perfil «{perfil}»:\n\n"
                    + "• Archivo de log en disco\n"
                    + "• Telemetría IPC (Monitor, actividad, estadísticas)\n"
                    + "• Comandos pendientes (control.json)\n"
                    + "• Contadores copiados/errores en la configuración del perfil\n\n"
                    + "No se borran los pares ni los archivos ya copiados en destino.",
                TextWrapping = TextWrapping.WrapWholeWords
            },
            PrimaryButtonText = "Limpiar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.Window.Content.XamlRoot
        };

        if (await cuadro.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var resultado = ServicioLimpiezaDatosPerfil.Limpiar(perfil);

        foreach (var fila in Pares)
        {
            fila.TotalCopiados = 0;
            fila.TotalErrores = 0;
        }

        LeerConfiguracionDesdeUi();
        foreach (var par in _configuracion.Pares)
        {
            par.TotalCopiados = 0;
            par.TotalErrores = 0;
        }

        _servicioConfig.Guardar(_rutaConfig, _configuracion);
        MarcarComoGuardado();

        ReiniciarRegistroPerfil();
        Estadisticas.ReiniciarPresentacion();
        LimpiarVistaTelemetria();

        var resumen = resultado.ElementosLimpiados.Count > 0
            ? "Eliminado o vaciado:\n• " + string.Join("\n• ", resultado.ElementosLimpiados)
            : "No había archivos de telemetría ni log que limpiar.";

        if (resultado.Errores.Count > 0)
        {
            resumen += "\n\nAdvertencias:\n• " + string.Join("\n• ", resultado.Errores);
        }

        resumen += "\n\nContadores del perfil reiniciados a cero.";

        await MostrarAvisoAsync(
            resumen,
            resultado.Exito ? "Limpieza completada" : "Limpieza parcial");
    }

    private string PerfilActual() =>
        string.IsNullOrWhiteSpace(PerfilSeleccionado) ? "General" : PerfilSeleccionado.Trim();

    private void CargarListaPerfiles(string? excluirPerfil = null)
    {
        var actual = PerfilActual();
        Perfiles.Clear();
        var lista = _servicioConfig.ListarPerfiles()
            .Where(p => excluirPerfil is null || !p.Equals(excluirPerfil, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!lista.Contains(actual, StringComparer.OrdinalIgnoreCase)
            && (excluirPerfil is null || !actual.Equals(excluirPerfil, StringComparison.OrdinalIgnoreCase)))
        {
            lista.Add(actual);
        }

        foreach (var p in lista.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            Perfiles.Add(p);
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private async Task ElegirRutaConfigExistenteAsync()
    {
        if (DemonioEnEjecucion)
        {
            await MostrarAvisoAsync("Detén el demonio antes de cambiar la ubicación del JSON.", "Demonio activo");
            return;
        }

        var elegida = await ServicioSelectorArchivoJson.ElegirArchivoExistenteAsync(_rutaConfig);
        if (string.IsNullOrWhiteSpace(elegida))
        {
            return;
        }

        try
        {
            _servicioConfig.EstablecerRutaPersonalizada(PerfilActual(), elegida);
            CargarConfiguracionPerfilActual();
            await MostrarAvisoAsync($"El perfil usará:\n{elegida}", "Configuración personalizada");
        }
        catch (Exception ex)
        {
            await MostrarAvisoAsync(ex.Message, "No se pudo cambiar la ruta");
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private async Task CrearRutaConfigPersonalizadaAsync()
    {
        if (DemonioEnEjecucion)
        {
            await MostrarAvisoAsync("Detén el demonio antes de cambiar la ubicación del JSON.", "Demonio activo");
            return;
        }

        var destino = await ServicioSelectorArchivoJson.ElegirRutaGuardadoAsync(_rutaConfig);
        if (string.IsNullOrWhiteSpace(destino))
        {
            return;
        }

        try
        {
            LeerConfiguracionDesdeUi();
            _servicioConfig.Guardar(destino, _configuracion);
            _servicioConfig.EstablecerRutaPersonalizada(PerfilActual(), destino);
            CargarConfiguracionPerfilActual();
            await MostrarAvisoAsync($"Configuración guardada en:\n{destino}", "Ubicación personalizada");
        }
        catch (Exception ex)
        {
            await MostrarAvisoAsync(ex.Message, "No se pudo crear la configuración");
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private async Task RestablecerRutaConfigPorDefectoAsync()
    {
        if (DemonioEnEjecucion)
        {
            await MostrarAvisoAsync("Detén el demonio antes de cambiar la ubicación del JSON.", "Demonio activo");
            return;
        }

        if (!UsaRutaConfigPersonalizada)
        {
            await MostrarAvisoAsync("Este perfil ya usa la ubicación por defecto.", "SManager 2.0");
            return;
        }

        var cuadro = new ContentDialog
        {
            Title = "Restaurar ubicación por defecto",
            Content = new TextBlock
            {
                Text =
                    "El perfil volverá a leer y guardar en la carpeta estándar de Perfiles configuracion.\n\n"
                    + "El archivo personalizado no se borra del disco.",
                TextWrapping = TextWrapping.WrapWholeWords
            },
            PrimaryButtonText = "Restaurar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.Window.Content.XamlRoot
        };

        if (await cuadro.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            _servicioConfig.RestablecerRutaPorDefecto(PerfilActual());
            CargarConfiguracionPerfilActual();
            await MostrarAvisoAsync($"Ubicación por defecto:\n{RutaConfiguracionPorDefecto}", "Restaurado");
        }
        catch (Exception ex)
        {
            await MostrarAvisoAsync(ex.Message, "No se pudo restaurar");
        }
    }

    private void CargarConfiguracionPerfilActual()
    {
        _suprimirMarcadoSucio = true;
        try
        {
            CargarConfiguracionPerfilActualInterno();
            MarcarComoGuardado();
        }
        finally
        {
            _suprimirMarcadoSucio = false;
        }
    }

    private void CargarConfiguracionPerfilActualInterno()
    {
        var perfil = PerfilActual();
        _rutaConfig = _servicioConfig.AsegurarConfiguracionPerfil(perfil);
        RutaConfiguracion = _rutaConfig;
        RutaConfiguracionPorDefecto = _servicioConfig.ObtenerRutaPorDefecto(perfil);
        UsaRutaConfigPersonalizada = _servicioConfig.UsaRutaPersonalizada(perfil);
        TextoModoRutaConfig = UsaRutaConfigPersonalizada ? "Modo: ubicación personalizada" : "Modo: ubicación por defecto";
        _configuracion = _servicioConfig.Cargar(_rutaConfig);

        Pares.Clear();
        foreach (var par in _configuracion.Pares)
        {
            Pares.Add(new ParFilaViewModel
            {
                IdPar = par.IdPar,
                Habilitado = par.Habilitado,
                Pausado = par.Pausado,
                Nombre = par.Nombre,
                RutaOrigen = par.RutaOrigen,
                RutaDestino = par.RutaDestino,
                FiltroInclusion = par.FiltroInclusion,
                FiltroExclusion = par.FiltroExclusion,
                TotalCopiados = par.TotalCopiados,
                TotalErrores = par.TotalErrores
            });
        }

        IntervaloPollingSegundos = _configuracion.IntervaloPollingSegundos;
        SegundosEstabilidadArchivo = _configuracion.SegundosEstabilidadArchivo;
        NumCopiadoresParalelos = _configuracion.NumCopiadoresParalelos;
        NumHidratadoresParalelos = _configuracion.NumHidratadoresParalelos;
        TimeoutHidratacionSegundos = _configuracion.TimeoutHidratacionSegundos;
        IntervaloPublicacionEstadoMs = _configuracion.IntervaloPublicacionEstadoMs;
        ActualizarFiltrosParRegistro();
        ActualizarEstadoVacioPares();
        ActualizarPanelInicio(null);
    }

    /// <summary>Vacía paneles que dependen de telemetría IPC en vivo.</summary>
    private void LimpiarVistaTelemetria()
    {
        TextoResumen = "Demonio detenido — sin telemetría en vivo.";
        TextoPolling = "—";
        MonitorPares.Clear();
        CopiasEnCurso.Clear();
        ActividadReciente.Clear();
        ActualizarPanelInicio(null);
    }

    /// <summary>Actualiza el dashboard de Inicio con datos legibles para el usuario.</summary>
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
            TextoColaHumana = "Pulsa Iniciar para comenzar la sincronización unidireccional.";
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

    private void LeerConfiguracionDesdeUi()
    {
        _configuracion.IntervaloPollingSegundos = IntervaloPollingSegundos;
        _configuracion.SegundosEstabilidadArchivo = SegundosEstabilidadArchivo;
        _configuracion.NumCopiadoresParalelos = NumCopiadoresParalelos;
        _configuracion.NumHidratadoresParalelos = NumHidratadoresParalelos;
        _configuracion.TimeoutHidratacionSegundos = TimeoutHidratacionSegundos;
        _configuracion.IntervaloPublicacionEstadoMs = IntervaloPublicacionEstadoMs;

        _configuracion.Pares = Pares.Select(fila => new ParSincronizacion
        {
            IdPar = fila.IdPar,
            Habilitado = fila.Habilitado,
            Pausado = fila.Pausado,
            Nombre = fila.Nombre,
            RutaOrigen = fila.RutaOrigen,
            RutaDestino = fila.RutaDestino,
            FiltroInclusion = fila.FiltroInclusion,
            FiltroExclusion = fila.FiltroExclusion,
            TotalCopiados = fila.TotalCopiados,
            TotalErrores = fila.TotalErrores
        }).ToList();
    }

    private List<string> EvaluarRutas(out bool todasValidas)
    {
        var errores = new List<string>();
        foreach (var fila in Pares.Where(p => p.Habilitado))
        {
            if (string.IsNullOrWhiteSpace(fila.RutaOrigen) || string.IsNullOrWhiteSpace(fila.RutaDestino))
            {
                errores.Add($"- '{fila.Nombre}': origen o destino vacíos.");
            }
            else if (!Directory.Exists(fila.RutaOrigen) || !Directory.Exists(fila.RutaDestino))
            {
                errores.Add($"- '{fila.Nombre}': rutas inaccesibles.");
            }
        }

        todasValidas = errores.Count == 0;
        return errores;
    }

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

    /// <summary>Detecta inicio/fin de sesión del demonio para persistir historial.</summary>
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
                ? Microsoft.UI.Colors.MediumSeaGreen
                : Microsoft.UI.Colors.IndianRed);
        OnPropertyChanged(nameof(ColorEstado));
        ActualizarBandejaSistema();
    }

    /// <summary>
    /// Tras operaciones async el hilo puede no ser el de la UI; WinUI solo refresca botones de forma fiable en el hilo UI.
    /// </summary>
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
                    ? $"Próximo barrido de seguridad: en {estado.ProximoPollingEnSegundos}s"
                    : "Próximo barrido de seguridad: —";

                ServicioSincronizacionLista.SincronizarInPlace(
                    MonitorPares,
                    estado.Pares.Count,
                    (indice, fila) =>
                    {
                        var par = estado.Pares[indice];
                        fila.ActualizarDesde(par.Nombre, par.Estado, par.Copiados, par.Errores);
                    },
                    indice =>
                    {
                        var par = estado.Pares[indice];
                        return MonitorParViewModel.Crear(par.Nombre, par.Estado, par.Copiados, par.Errores);
                    });

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
            }
            else
            {
                LimpiarVistaTelemetria();
            }

            ActualizarPanelInicio(estado);

            _monitorNotificaciones.Evaluar(
                NotificacionesHabilitadas,
                perfil,
                DemonioEnEjecucion,
                estado?.Totales.Copiados ?? 0,
                estado?.Totales.Errores ?? 0);

            ActualizarBandejaSistema();

            // El log se lee del disco aunque el demonio esté detenido.
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

    private static async Task MostrarAvisoAsync(string mensaje, string titulo)
    {
        var cuadro = new ContentDialog
        {
            Title = titulo,
            Content = new TextBlock { Text = mensaje, TextWrapping = TextWrapping.WrapWholeWords },
            CloseButtonText = "Aceptar",
            XamlRoot = App.Window.Content.XamlRoot
        };
        await cuadro.ShowAsync();
    }

    public void Dispose()
    {
        _temporizador?.Stop();
        _bandeja?.Dispose();
        _bandeja = null;
    }
}
