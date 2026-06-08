using System.Collections.ObjectModel;
using System.Text;
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

    /// <summary>La vista pide desplazarse al final del registro (carga inicial o líneas nuevas).</summary>
    public event EventHandler? RegistroDesplazarAlFinalSolicitado;

    public ObservableCollection<string> Perfiles { get; } = [];
    public ObservableCollection<string> FiltrosParRegistro { get; } = [];
    public ObservableCollection<ParFilaViewModel> Pares { get; } = [];
    public ObservableCollection<MonitorParViewModel> MonitorPares { get; } = [];
    public ObservableCollection<CopiaEnCursoViewModel> CopiasEnCurso { get; } = [];
    public ObservableCollection<ActividadViewModel> ActividadReciente { get; } = [];

    [ObservableProperty]
    private string _perfilSeleccionado = "General";

    [ObservableProperty]
    private string _rutaConfiguracion = string.Empty;

    [ObservableProperty]
    private string _textoEstado = "Detenido";

    /// <summary>Indicador visual junto al estado en la barra superior.</summary>
    public SolidColorBrush ColorEstado { get; private set; } = new(Microsoft.UI.Colors.IndianRed);

    [ObservableProperty]
    private string _textoResumen = string.Empty;

    [ObservableProperty]
    private string _textoPolling = string.Empty;

    [ObservableProperty]
    private string _textoRegistro = string.Empty;

    [ObservableProperty]
    private string _parFiltroRegistroSeleccionado = ServicioFiltradoRegistro.EtiquetaTodosLosPares;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IniciarCommand))]
    [NotifyCanExecuteChangedFor(nameof(DetenerCommand))]
    [NotifyCanExecuteChangedFor(nameof(RecargarCommand))]
    private bool _demonioEnEjecucion;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IniciarCommand))]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    [NotifyCanExecuteChangedFor(nameof(NuevoPerfilCommand))]
    [NotifyCanExecuteChangedFor(nameof(AnadirParCommand))]
    [NotifyCanExecuteChangedFor(nameof(QuitarParCommand))]
    private bool _puedeEditarConfig = true;

    [ObservableProperty]
    private ParFilaViewModel? _parSeleccionado;

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
        ActualizarEstadoBotones();
        ReiniciarRegistroPerfil();
        _temporizador.Start();
    }

    partial void OnParFiltroRegistroSeleccionadoChanged(string value) =>
        ReaplicarFiltroRegistro(moverAlFinal: false);

    [RelayCommand]
    private void CambiarPerfil()
    {
        LeerConfiguracionDesdeUi();
        CargarConfiguracionPerfilActual();
        ActualizarEstadoBotones();
        ReiniciarRegistroPerfil();
    }

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private async Task NuevoPerfilAsync()
    {
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
            Text = "General",
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
                    Text = "Se creará una carpeta en Perfiles configuracion:",
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
        LeerConfiguracionDesdeUi();
        _servicioConfig.Guardar(_rutaConfig, _configuracion);
        await MostrarAvisoAsync($"Configuración guardada en:\n{_rutaConfig}", "SManager 2.0");
    }

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private async Task IniciarAsync()
    {
        try
        {
            LeerConfiguracionDesdeUi();
            _servicioConfig.Guardar(_rutaConfig, _configuracion);

            var errores = EvaluarRutas(out var ok);
            if (!ok)
            {
                await MostrarAvisoAsync(string.Join('\n', errores), "Rutas no válidas");
                return;
            }

            var (codigo, salida, error) = await _daemon.EjecutarAsync(
                $"start -perfil \"{PerfilActual()}\" -configpath \"{_rutaConfig}\"");

            if (codigo != 0)
            {
                await MostrarAvisoAsync(string.IsNullOrWhiteSpace(error) ? salida : error, "Error al iniciar");
            }

            _posicionLog = 0;
            _textoRegistroCrudo = string.Empty;
            TextoRegistro = string.Empty;
            CargarListaPerfiles();
        }
        finally
        {
            await RefrescarEstadoDemonioEnUiAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(DemonioEnEjecucion))]
    private async Task DetenerAsync()
    {
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
        }
    }

    [RelayCommand(CanExecute = nameof(DemonioEnEjecucion))]
    private async Task RecargarAsync()
    {
        var (codigo, salida, error) = await _daemon.EjecutarAsync($"reload -perfil \"{PerfilActual()}\"");
        if (codigo != 0)
        {
            await MostrarAvisoAsync(string.IsNullOrWhiteSpace(error) ? salida : error, "Error al recargar");
            return;
        }

        CargarConfiguracionPerfilActual();
    }

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private void AnadirPar()
    {
        var par = new ParFilaViewModel();
        Pares.Add(par);
        ParSeleccionado = par;
        ActualizarFiltrosParRegistro();
    }

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private void QuitarPar()
    {
        if (ParSeleccionado is null)
        {
            return;
        }

        Pares.Remove(ParSeleccionado);
        ParSeleccionado = null;
        ActualizarFiltrosParRegistro();
    }

    [RelayCommand]
    private async Task ValidarRutasAsync()
    {
        var errores = EvaluarRutas(out var ok);
        await MostrarAvisoAsync(
            ok ? "Todas las rutas existen." : string.Join('\n', errores),
            "Validación");
    }

    private string PerfilActual() =>
        string.IsNullOrWhiteSpace(PerfilSeleccionado) ? "General" : PerfilSeleccionado.Trim();

    private void CargarListaPerfiles()
    {
        var actual = PerfilActual();
        Perfiles.Clear();
        var lista = _servicioConfig.ListarPerfiles().ToList();
        if (!lista.Contains(actual, StringComparer.OrdinalIgnoreCase))
        {
            lista.Add(actual);
        }

        foreach (var p in lista.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            Perfiles.Add(p);
        }
    }

    private void CargarConfiguracionPerfilActual()
    {
        var perfil = PerfilActual();
        _rutaConfig = _servicioConfig.CrearPerfil(perfil);
        RutaConfiguracion = _rutaConfig;
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
    }

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

    private void ActualizarEstadoBotones()
    {
        DemonioEnEjecucion = _ipc.EstaDemonioEnEjecucion(PerfilActual());
        PuedeEditarConfig = !DemonioEnEjecucion;
        TextoEstado = DemonioEnEjecucion ? "Sincronizando" : "Detenido";
        ColorEstado = new SolidColorBrush(
            DemonioEnEjecucion
                ? Microsoft.UI.Colors.MediumSeaGreen
                : Microsoft.UI.Colors.IndianRed);
        OnPropertyChanged(nameof(ColorEstado));
        NotificarComandosAccion();
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

    private void NotificarComandosAccion()
    {
        IniciarCommand.NotifyCanExecuteChanged();
        DetenerCommand.NotifyCanExecuteChanged();
        RecargarCommand.NotifyCanExecuteChanged();
        GuardarCommand.NotifyCanExecuteChanged();
        NuevoPerfilCommand.NotifyCanExecuteChanged();
        AnadirParCommand.NotifyCanExecuteChanged();
        QuitarParCommand.NotifyCanExecuteChanged();
    }

    private async Task ActualizarVistaAsync()
    {
        ActualizarEstadoBotones();
        var perfil = PerfilActual();
        var estado = await _ipc.LeerEstadoAsync(perfil);

        App.DispatcherQueue.TryEnqueue(() =>
        {
            if (estado is not null)
            {
                TextoResumen =
                    $"Cola: {estado.ColaCopiaPendiente}  Únicos: {estado.ArchivosUnicosPendientes}  Dup.ev: {estado.DuplicadosEvitados}  Copiados: {estado.Totales.Copiados}  Errores: {estado.Totales.Errores}";
                TextoPolling = estado.ProximoPollingEnSegundos.HasValue
                    ? $"Próximo polling: en {estado.ProximoPollingEnSegundos}s"
                    : "Próximo polling: —";

                SincronizarColeccion(MonitorPares, estado.Pares.Select(p => new MonitorParViewModel
                {
                    Nombre = p.Nombre,
                    Estado = p.Estado,
                    Copiados = p.Copiados,
                    Errores = p.Errores
                }));

                SincronizarColeccion(CopiasEnCurso, estado.CopiasEnCurso.Select(c => new CopiaEnCursoViewModel
                {
                    Copiador = c.Copiador,
                    Archivo = c.Archivo,
                    IdPar = c.IdPar
                }));

                SincronizarColeccion(ActividadReciente, estado.ActividadReciente.Select(a => new ActividadViewModel
                {
                    Hora = a.Hora,
                    Tipo = a.Tipo,
                    Archivo = a.Archivo,
                    IdPar = a.IdPar
                }));
            }

            // El log se lee del disco aunque el demonio esté detenido.
            ActualizarRegistro(perfil);
        });
    }

    private void ReiniciarRegistroPerfil()
    {
        _posicionLog = 0;
        _textoRegistroCrudo = string.Empty;
        TextoRegistro = string.Empty;
        ActualizarFiltrosParRegistro();
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

    private void ReaplicarFiltroRegistro(bool moverAlFinal)
    {
        TextoRegistro = ServicioFiltradoRegistro.Filtrar(
            _textoRegistroCrudo,
            ParFiltroRegistroSeleccionado,
            ConstruirMapaIdNombrePar());

        if (moverAlFinal)
        {
            RegistroDesplazarAlFinalSolicitado?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ActualizarRegistro(string perfil, bool moverAlFinal = false)
    {
        var rutaLog = RutasDatos.ObtenerRutaLog(perfil);
        if (!File.Exists(rutaLog))
        {
            return;
        }

        try
        {
            using var flujo = new FileStream(rutaLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (flujo.Length < _posicionLog)
            {
                _posicionLog = 0;
                _textoRegistroCrudo = string.Empty;
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
                }

                moverAlFinal = true;
            }

            ReaplicarFiltroRegistro(moverAlFinal);
        }
        catch
        {
            // No bloquear la UI por el log.
        }
    }

    private static void SincronizarColeccion<T>(ObservableCollection<T> destino, IEnumerable<T> origen)
    {
        destino.Clear();
        foreach (var item in origen)
        {
            destino.Add(item);
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
    }
}
