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

    [ObservableProperty]
    private bool _expandido;

    public int TotalCopiados { get; set; }

    public int TotalErrores { get; set; }

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

    partial void OnExpandidoChanged(bool value)
    {
        OnPropertyChanged(nameof(GlifoExpansion));
    }

    /// <summary>Glifo MDL2 para expandir/colapsar la tarjeta.</summary>
    public string GlifoExpansion => Expandido ? "\uE70E" : "\uE70D";

    partial void OnRutaOrigenChanged(string value) => NotificarResumen();

    partial void OnRutaDestinoChanged(string value) => NotificarResumen();

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
        FiltroExclusion = FiltroExclusion
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
