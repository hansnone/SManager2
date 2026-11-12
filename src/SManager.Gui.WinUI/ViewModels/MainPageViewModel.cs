using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SManager.Core.Modelos;
using SManager.Gui.Shared;
using SManager.Gui.WinUI.Models;
using SManager.Gui.WinUI.Servicios;
using SManager.Ipc;

namespace SManager.Gui.WinUI.ViewModels;

/// <summary>
/// ViewModel principal del panel de control SManager 2.0.
/// Dividido en clases parciales por responsabilidad (Perfiles, Pares, Telemetría) para mantener cada archivo claro y manejable.
/// </summary>
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

    /// <summary>La vista principal reacciona al cambiar modo básico/avanzado.</summary>
    public event EventHandler? ModoInterfazCambiado;

    /// <summary>Solicita a la vista principal navegar a una sección por su etiqueta ("pares", "monitor", etc.).</summary>
    public event EventHandler<string>? NavegarSeccionSolicitado;

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

    /// <summary>Pares con estado OK mientras el demonio está activo (para exportar imagen).</summary>
    public ObservableCollection<MonitorParViewModel> MonitorParesFuncionando { get; } = [];

    /// <summary>Instantánea enriquecida para la captura PNG/JPG (origen + destino + estadísticas).</summary>
    public ObservableCollection<ParExportacionImagenViewModel> ParesExportacionImagen { get; } = [];

    public ObservableCollection<CopiaEnCursoViewModel> CopiasEnCurso { get; } = [];
    public ObservableCollection<ActividadViewModel> ActividadReciente { get; } = [];
    public ObservableCollection<HistorialSesionViewModel> HistorialSesiones { get; } = [];

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
    private bool _mostrarEstadoVacioCopiasEnCurso = true;

    [ObservableProperty]
    private bool _mostrarEstadoVacioActividadReciente = true;

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

    public IReadOnlyList<string> OpcionesTema => ServicioTemaAplicacion.Opciones;

    public IReadOnlyList<string> OpcionesIdioma => ServicioTextosUi.OpcionesIdiomaEtiqueta;

    /// <summary>Habilita operaciones estructurales del perfil (eliminar perfil, cambiar ruta JSON) solo con demonio detenido.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NuevoPerfilCommand))]
    [NotifyCanExecuteChangedFor(nameof(EliminarPerfilCommand))]
    private bool _puedeEditarConfig = true;

    /// <summary>Habilita Iniciar en XAML.</summary>
    [ObservableProperty]
    private bool _puedeIniciar = true;

    /// <summary>Habilita Detener/Recargar en XAML.</summary>
    [ObservableProperty]
    private bool _puedeDetener;

    [ObservableProperty]
    private bool _demonioEnEjecucion;

    [ObservableProperty]
    private bool _sesionBorradoDesbloqueada;

    [ObservableProperty]
    private ParFilaViewModel? _parSeleccionado;

    /// <summary>True cuando no hay pares: la vista muestra estado vacío con botón de acción.</summary>
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

    [ObservableProperty]
    private bool _autoInicioIniciarDemonio = true;

    private bool _cargandoPreferenciasAutoInicio;
    private ServicioBandejaSistema? _bandeja;
    private readonly ServicioTamanoDirectorio _servicioTamanoDirectorio = new();
    private CancellationTokenSource? _cancelacionTamanosDestino;
    private PreferenciasColumnasDto _preferenciasColumnas = new();
    private DispatcherQueueTimer? _temporizadorRecalculoTamanos;
    private DateTimeOffset _ultimoRecalculoTamanosAutomatico = DateTimeOffset.MinValue;

    private static readonly TimeSpan RetardoRecalculoTrasSesion = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan IntervaloMinimoRecalculoAutomatico = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PausaEntreParesRecalculoAutomatico = TimeSpan.FromMilliseconds(400);

    [ObservableProperty]
    private double _anchoRegistroHora = 140;

    [ObservableProperty]
    private double _anchoRegistroPar = 120;

    [ObservableProperty]
    private double _anchoRegistroNivel = 72;

    [ObservableProperty]
    private double _anchoRegistroMensaje = 360;

    [ObservableProperty]
    private double _anchoMonitorNombre = 160;

    [ObservableProperty]
    private double _anchoMonitorEstado = 100;

    [ObservableProperty]
    private double _anchoMonitorTamanoDestino = 120;

    [ObservableProperty]
    private double _anchoMonitorCopiados = 88;

    [ObservableProperty]
    private double _anchoMonitorErrores = 72;

    [ObservableProperty]
    private string _textoExportacionPerfil = string.Empty;

    [ObservableProperty]
    private string _textoExportacionFecha = string.Empty;

    [ObservableProperty]
    private bool _calculandoTamanosDestino;

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

    /// <summary>Arranca los temporizadores, carga el perfil activo y aplica el tema visual.</summary>
    public void Inicializar()
    {
        if (_temporizador is null)
        {
            _temporizador = App.DispatcherQueue.CreateTimer();
            _temporizador.Interval = TimeSpan.FromMilliseconds(500);
            _temporizador.Tick += async (_, _) => await ActualizarVistaAsync();
        }

        CargarPreferenciasColumnas();
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

        if (!App.IniciarOcultoEnBandeja
            && ServicioOnboarding.DebeMostrarAsistenteAutomatico(Pares.Count > 0))
        {
            MostrarAsistenteOnboarding = true;
        }
        else if (!App.IniciarOcultoEnBandeja)
        {
            IntentarMostrarConsejosIniciales();
        }

        _ = AplicarPreferenciasArranqueWindowsAsync();
    }

    private async Task AplicarPreferenciasArranqueWindowsAsync()
    {
        if (App.IniciarOcultoEnBandeja)
        {
            await Task.Delay(350);

            if (BandejaHabilitada || _bandeja is not null)
            {
                App.Window.AppWindow.Hide();
                NotificarOcultadoEnBandeja();
            }
            else if (App.Window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presentador)
            {
                presentador.Minimize();
            }
        }

        if (App.ArranqueConDemonio && PuedeIniciar)
        {
            await IniciarAsync();
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

        var necesitaBandeja = BandejaHabilitada || App.IniciarOcultoEnBandeja;
        if (!necesitaBandeja)
        {
            return;
        }

        try
        {
            var rutaIcono = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            _bandeja = new ServicioBandejaSistema();
            _bandeja.Inicializar(rutaIcono);
            ActualizarBandejaSistema();

            ServicioSenalRestaurarVentana.IniciarEscucha(
                ServicioAccionesBandeja.SolicitarAbrirVentana);
        }
        catch
        {
            _bandeja?.Dispose();
            _bandeja = null;
        }
    }

    private void ActualizarBandejaSistema()
    {
        if (_bandeja is null)
        {
            return;
        }

        var tooltip = DemonioEnEjecucion
            ? $"SManager — Sincronizando ({PerfilActual()})"
            : $"SManager — Detenido ({PerfilActual()})";

        _bandeja.ActualizarEstado(tooltip, PuedeIniciar, PuedeDetener);
    }

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

        if (resultado.GuardarConfiguracion)
        {
            await GuardarYRecargarSiActivoAsync();
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
        _cancelacionTamanosDestino?.Cancel();
        _cancelacionTamanosDestino?.Dispose();
        _temporizadorRecalculoTamanos?.Stop();
        ServicioSenalRestaurarVentana.DetenerEscucha();
        _temporizador?.Stop();
        _bandeja?.Dispose();
        _bandeja = null;
    }
}
