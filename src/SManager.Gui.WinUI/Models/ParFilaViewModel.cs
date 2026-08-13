using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SManager.Core.Modelos;
using SManager.Gui.Shared.Servicios;

namespace SManager.Gui.WinUI.Models;

/// <summary>Par de sincronización mostrado como tarjeta en la GUI.</summary>
public partial class ParFilaViewModel : ObservableObject
{
    public string IdPar { get; init; } = Guid.NewGuid().ToString();

    [ObservableProperty]
    private bool _habilitado = true;

    [ObservableProperty]
    private bool _pausado;

    [ObservableProperty]
    private string _nombre = "Nuevo par";

    [ObservableProperty]
    private string _rutaOrigen = string.Empty;

    [ObservableProperty]
    private string _rutaDestino = string.Empty;

    [ObservableProperty]
    private string _filtroInclusion = "*";

    [ObservableProperty]
    private string _filtroExclusion = "~$*;*.tmp;*.partial;*.lnk";

    /// <summary>
    /// Segundos entre barridos de seguridad de este par.
    /// Null = heredar el valor global de Ajustes.
    /// </summary>
    [ObservableProperty]
    private int? _intervaloPollingSegundos;

    [ObservableProperty]
    private bool _seleccionado;

    [ObservableProperty]
    private ModoSincronizacion _modo = ModoSincronizacion.AcumulativoSinBorrado;

    [ObservableProperty]
    private bool _borrarEnOrigen;

    [ObservableProperty]
    private bool _expandido;

    [ObservableProperty]
    private bool _tienePurgaMasivaBloqueada;

    [ObservableProperty]
    private int _cantidadArchivosPurgaBloqueada;

    public string TextoAlertaPurgaMasiva =>
        $"Purga masiva en Modo Espejo pausada por seguridad: {CantidadArchivosPurgaBloqueada} archivos pendientes de eliminar en destino.";

    public int TotalCopiados { get; set; }

    public int TotalErrores { get; set; }

    [ObservableProperty]
    private long _tamanoDestinoBytes;

    [ObservableProperty]
    private string _tamanoDestinoTexto = "—";

    [ObservableProperty]
    private bool _tamanoDestinoCalculando;

    /// <summary>True si hay avisos de riesgo en la configuración del par.</summary>
    public bool TieneAvisosRiesgo => AvisosRiesgo.Count > 0;

    /// <summary>Avisos preventivos (origen=destino, destino dentro de origen, etc.).</summary>
    public IReadOnlyList<string> AvisosRiesgo { get; private set; } = [];

    /// <summary>Primer aviso para mostrar en tarjeta compacta.</summary>
    public string TextoPrimerAviso => AvisosRiesgo.FirstOrDefault() ?? string.Empty;

    /// <summary>Resumen corto origen → destino para la cabecera de la tarjeta.</summary>
    public string ResumenRutas
    {
        get
        {
            var origen = string.IsNullOrWhiteSpace(RutaOrigen) ? "—" : Acortar(RutaOrigen, 42);
            var destino = string.IsNullOrWhiteSpace(RutaDestino) ? "—" : Acortar(RutaDestino, 42);
            return $"{origen}  →  {destino}";
        }
    }

    /// <summary>Etiqueta legible del estado activo/pausado/inactivo.</summary>
    public string EtiquetaEstadoActividad =>
        !Habilitado ? "Inactivo" : Pausado ? "Pausado" : "Activo";

    /// <summary>Clave para el convertidor de tema (OK, PAUSADO, INACTIVO).</summary>
    public string ClaveEstadoChip =>
        !Habilitado ? "INACTIVO" : Pausado ? "PAUSADO" : "OK";

    /// <summary>Texto del chip visual del modo de sincronización activo.</summary>
    public string EtiquetaModoSincronizacion => Modo switch
    {
        ModoSincronizacion.AcumulativoConBorradoOrigen => "Borrado en Origen",
        ModoSincronizacion.Espejo => "Espejo (Mirror)",
        _ => "Acumulativo"
    };

    partial void OnExpandidoChanged(bool value)
    {
        OnPropertyChanged(nameof(GlifoExpansion));
    }

    /// <summary>Glifo MDL2 para expandir/colapsar la tarjeta.</summary>
    public string GlifoExpansion => Expandido ? "\uE70E" : "\uE70D";

    partial void OnRutaOrigenChanged(string value) => NotificarResumen();

    partial void OnRutaDestinoChanged(string value)
    {
        NotificarResumen();
        TamanoDestinoTexto = "—";
        TamanoDestinoBytes = 0;
    }

    partial void OnNombreChanged(string value) => OnPropertyChanged(nameof(EtiquetaEstadoActividad));

    partial void OnHabilitadoChanged(bool value) => NotificarEstadoActividad();

    partial void OnPausadoChanged(bool value) => NotificarEstadoActividad();

    partial void OnFiltroInclusionChanged(string value) => ActualizarAvisosRiesgo();

    partial void OnFiltroExclusionChanged(string value) => ActualizarAvisosRiesgo();

    [RelayCommand]
    private void AlternarExpansion() => Expandido = !Expandido;

    /// <summary>Recalcula avisos cuando cambian rutas o filtros.</summary>
    public void ActualizarAvisosRiesgo()
    {
        AvisosRiesgo = ServicioValidacionRiesgoPar.DetectarAvisos(ComoModeloCore());
        OnPropertyChanged(nameof(AvisosRiesgo));
        OnPropertyChanged(nameof(TieneAvisosRiesgo));
        OnPropertyChanged(nameof(TextoPrimerAviso));
    }

    private ParSincronizacion ComoModeloCore() => new()
    {
        IdPar = IdPar,
        Nombre = Nombre,
        Habilitado = Habilitado,
        Pausado = Pausado,
        RutaOrigen = RutaOrigen,
        RutaDestino = RutaDestino,
        FiltroInclusion = FiltroInclusion,
        FiltroExclusion = FiltroExclusion,
        IntervaloPollingSegundos = IntervaloPollingSegundos,
        Modo = Modo,
        BorrarEnOrigen = Modo == ModoSincronizacion.AcumulativoConBorradoOrigen
    };

    private void NotificarResumen()
    {
        OnPropertyChanged(nameof(ResumenRutas));
        ActualizarAvisosRiesgo();
    }

    private void NotificarEstadoActividad()
    {
        OnPropertyChanged(nameof(EtiquetaEstadoActividad));
        OnPropertyChanged(nameof(ClaveEstadoChip));
    }

    private static string Acortar(string texto, int maximo) =>
        texto.Length <= maximo ? texto : texto[..(maximo - 1)] + "…";
}
