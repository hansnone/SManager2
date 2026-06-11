using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SManager.Core.Modelos;
using SManager.Gui.WinUI.Models;
using SManager.Gui.WinUI.Servicios;

namespace SManager.Gui.WinUI.Vistas;

/// <summary>Formulario reutilizable para crear o editar un par dentro de un ContentDialog.</summary>
public sealed partial class DialogoParControl : UserControl
{
    private readonly ObservableCollection<ReglaFiltroViewModel> _reglasExclusion = [];

    private string _idPar = Guid.NewGuid().ToString();

    /// <summary>Notifica al diálogo padre para habilitar o deshabilitar el botón principal.</summary>
    public event EventHandler? ValidezCambiada;

    public DialogoParControl()
    {
        InitializeComponent();
        ListaReglasExclusion.ItemsSource = _reglasExclusion;
        CajaNombre.TextChanged += (_, _) => NotificarValidez();
        CajaOrigen.TextChanged += (_, _) => NotificarValidez();
        CajaDestino.TextChanged += (_, _) => NotificarValidez();
    }

    /// <summary>True cuando nombre, origen y destino tienen texto (mínimo para aceptar el formulario).</summary>
    public bool EsValido() =>
        !string.IsNullOrWhiteSpace(CajaNombre.Text)
        && !string.IsNullOrWhiteSpace(CajaOrigen.Text)
        && !string.IsNullOrWhiteSpace(CajaDestino.Text);

    private void NotificarValidez() => ValidezCambiada?.Invoke(this, EventArgs.Empty);

    /// <summary>Rellena los campos con los datos del par existente o valores por defecto.</summary>
    public void Cargar(ParFilaViewModel? par, bool esEdicion)
    {
        TextoAyuda.Text = esEdicion
            ? "Modifica las rutas o filtros del par seleccionado."
            : "Define origen, destino y filtros del nuevo par de sincronización.";

        _reglasExclusion.Clear();

        if (par is null)
        {
            _idPar = Guid.NewGuid().ToString();
            CajaNombre.Text = "Nuevo par";
            CajaOrigen.Text = string.Empty;
            CajaDestino.Text = string.Empty;
            CajaInclusion.Text = "*";
            foreach (var regla in ServicioReglasFiltroVisual.DesdeCadenaExclusion("~$*;*.tmp;*.partial;*.lnk"))
            {
                _reglasExclusion.Add(regla);
            }

            InterruptorActivo.IsOn = true;
            InterruptorPausa.IsOn = false;
            CajaPollingPar.Value = 0;
            TextoResultadoPruebaFiltros.Text = string.Empty;
            return;
        }

        _idPar = par.IdPar;
        CajaNombre.Text = par.Nombre;
        CajaOrigen.Text = par.RutaOrigen;
        CajaDestino.Text = par.RutaDestino;
        CajaInclusion.Text = par.FiltroInclusion;
        foreach (var regla in ServicioReglasFiltroVisual.DesdeCadenaExclusion(par.FiltroExclusion))
        {
            _reglasExclusion.Add(regla);
        }

        InterruptorActivo.IsOn = par.Habilitado;
        InterruptorPausa.IsOn = par.Pausado;
        CajaPollingPar.Value = par.IntervaloPollingSegundos ?? 0;
        TextoResultadoPruebaFiltros.Text = string.Empty;
    }

    /// <summary>Lee los valores actuales del formulario como modelo editable.</summary>
    public ParFilaViewModel ObtenerPar() =>
        new()
        {
            IdPar = _idPar,
            Nombre = CajaNombre.Text.Trim(),
            RutaOrigen = CajaOrigen.Text.Trim(),
            RutaDestino = CajaDestino.Text.Trim(),
            FiltroInclusion = string.IsNullOrWhiteSpace(CajaInclusion.Text) ? "*" : CajaInclusion.Text.Trim(),
            FiltroExclusion = ServicioReglasFiltroVisual.HaciaCadenaExclusion(_reglasExclusion),
            Habilitado = InterruptorActivo.IsOn,
            Pausado = InterruptorPausa.IsOn,
            IntervaloPollingSegundos = CajaPollingPar.Value <= 0 ? null : (int)CajaPollingPar.Value
        };

    private async void BotonOrigen_Click(object sender, RoutedEventArgs e)
    {
        var ruta = await ServicioSelectorCarpeta.ElegirCarpetaAsync(CajaOrigen.Text, desdeDialogoModal: true);
        if (!string.IsNullOrEmpty(ruta))
        {
            CajaOrigen.Text = ruta;
        }
    }

    private async void BotonDestino_Click(object sender, RoutedEventArgs e)
    {
        var ruta = await ServicioSelectorCarpeta.ElegirCarpetaAsync(CajaDestino.Text, desdeDialogoModal: true);
        if (!string.IsNullOrEmpty(ruta))
        {
            CajaDestino.Text = ruta;
        }
    }

    private void BotonAnadirRegla_Click(object sender, RoutedEventArgs e)
    {
        _reglasExclusion.Add(new ReglaFiltroViewModel { Patron = "*.tmp" });
    }

    private void BotonEliminarRegla_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ReglaFiltroViewModel regla })
        {
            _reglasExclusion.Remove(regla);
        }
    }

    private async void BotonProbarFiltros_Click(object sender, RoutedEventArgs e)
    {
        var carpeta = string.IsNullOrWhiteSpace(CajaOrigen.Text)
            ? await ServicioSelectorCarpeta.ElegirCarpetaAsync(null, desdeDialogoModal: true)
            : CajaOrigen.Text.Trim();

        if (string.IsNullOrWhiteSpace(carpeta) || !Directory.Exists(carpeta))
        {
            TextoResultadoPruebaFiltros.Text = "Elige una carpeta origen válida para probar los filtros.";
            return;
        }

        var par = new ParSincronizacion
        {
            FiltroInclusion = string.IsNullOrWhiteSpace(CajaInclusion.Text) ? "*" : CajaInclusion.Text.Trim(),
            FiltroExclusion = ServicioReglasFiltroVisual.HaciaCadenaExclusion(_reglasExclusion)
        };

        var resultado = await Task.Run(() => ServicioReglasFiltroVisual.ProbarCarpeta(carpeta, par));
        TextoResultadoPruebaFiltros.Text =
            $"Revisados: {resultado.ArchivosRevisados:N0} — "
            + $"Se copiarían: {resultado.ArchivosCopiados:N0} — "
            + $"Se omitirían: {resultado.ArchivosOmitidos:N0}";
    }
}
