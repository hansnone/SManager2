using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SManager.Gui.WinUI.Models;
using SManager.Gui.WinUI.Servicios;

namespace SManager.Gui.WinUI.Vistas;

/// <summary>Formulario reutilizable para crear o editar un par dentro de un ContentDialog.</summary>
public sealed partial class DialogoParControl : UserControl
{
    private string _idPar = Guid.NewGuid().ToString();

    /// <summary>Notifica al diálogo padre para habilitar o deshabilitar el botón principal.</summary>
    public event EventHandler? ValidezCambiada;

    public DialogoParControl()
    {
        InitializeComponent();
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

        if (par is null)
        {
            _idPar = Guid.NewGuid().ToString();
            CajaNombre.Text = "Nuevo par";
            CajaOrigen.Text = string.Empty;
            CajaDestino.Text = string.Empty;
            CajaInclusion.Text = "*";
            CajaExclusion.Text = "~$*;*.tmp;*.partial;*.lnk";
            InterruptorActivo.IsOn = true;
            InterruptorPausa.IsOn = false;
            return;
        }

        _idPar = par.IdPar;
        CajaNombre.Text = par.Nombre;
        CajaOrigen.Text = par.RutaOrigen;
        CajaDestino.Text = par.RutaDestino;
        CajaInclusion.Text = par.FiltroInclusion;
        CajaExclusion.Text = par.FiltroExclusion;
        InterruptorActivo.IsOn = par.Habilitado;
        InterruptorPausa.IsOn = par.Pausado;
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
            FiltroExclusion = CajaExclusion.Text.Trim(),
            Habilitado = InterruptorActivo.IsOn,
            Pausado = InterruptorPausa.IsOn
        };

    private async void BotonOrigen_Click(object sender, RoutedEventArgs e)
    {
        var ruta = await ServicioSelectorCarpeta.ElegirCarpetaAsync(CajaOrigen.Text);
        if (!string.IsNullOrEmpty(ruta))
        {
            CajaOrigen.Text = ruta;
        }
    }

    private async void BotonDestino_Click(object sender, RoutedEventArgs e)
    {
        var ruta = await ServicioSelectorCarpeta.ElegirCarpetaAsync(CajaDestino.Text);
        if (!string.IsNullOrEmpty(ruta))
        {
            CajaDestino.Text = ruta;
        }
    }
}
