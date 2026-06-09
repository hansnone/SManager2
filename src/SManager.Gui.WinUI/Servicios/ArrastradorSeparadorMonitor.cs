using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>
/// Permite redimensionar dos filas de un Grid arrastrando un separador horizontal.
/// Sustituye GridSplitter externo (evita incompatibilidades en WinUI unpackaged).
/// </summary>
public sealed class ArrastradorSeparadorMonitor
{
    private readonly Grid _panel;
    private readonly RowDefinition _filaSuperior;
    private readonly RowDefinition _filaInferior;
    private readonly int _indiceFilaSuperior;
    private readonly int _indiceFilaInferior;
    private readonly Action? _alFinalizarArrastre;
    private readonly double _altoMinimo;

    private bool _arrastrando;
    private double _ultimaPosicionY;
    private double _altoSuperiorInicial;
    private double _altoInferiorInicial;

    public ArrastradorSeparadorMonitor(
        Grid panel,
        RowDefinition filaSuperior,
        RowDefinition filaInferior,
        int indiceFilaSuperior,
        int indiceFilaInferior,
        Action? alFinalizarArrastre = null,
        double altoMinimo = 72)
    {
        _panel = panel;
        _filaSuperior = filaSuperior;
        _filaInferior = filaInferior;
        _indiceFilaSuperior = indiceFilaSuperior;
        _indiceFilaInferior = indiceFilaInferior;
        _alFinalizarArrastre = alFinalizarArrastre;
        _altoMinimo = altoMinimo;
    }

    /// <summary>Enlaza eventos de puntero al control visual del separador.</summary>
    public void Enlazar(UIElement separador)
    {
        separador.PointerPressed += Separador_PointerPressed;
        separador.PointerMoved += Separador_PointerMoved;
        separador.PointerReleased += Separador_PointerReleased;
        separador.PointerCanceled += Separador_PointerReleased;
    }

    private void Separador_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not UIElement elemento)
        {
            return;
        }

        elemento.CapturePointer(e.Pointer);
        _arrastrando = true;
        _ultimaPosicionY = e.GetCurrentPoint(_panel).Position.Y;

        NormalizarFilasAPixeles();
        _altoSuperiorInicial = _filaSuperior.Height.Value;
        _altoInferiorInicial = _filaInferior.Height.Value;
    }

    private void Separador_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_arrastrando)
        {
            return;
        }

        var posicionY = e.GetCurrentPoint(_panel).Position.Y;
        var delta = posicionY - _ultimaPosicionY;
        if (Math.Abs(delta) < 0.5)
        {
            return;
        }

        var nuevoSuperior = Math.Max(_altoMinimo, _altoSuperiorInicial + delta);
        var nuevoInferior = Math.Max(_altoMinimo, _altoInferiorInicial - delta);

        // Si una fila choca con el mínimo, no forzar la otra a crecer de más.
        if (nuevoSuperior <= _altoMinimo || nuevoInferior <= _altoMinimo)
        {
            return;
        }

        _filaSuperior.Height = new GridLength(nuevoSuperior, GridUnitType.Pixel);
        _filaInferior.Height = new GridLength(nuevoInferior, GridUnitType.Pixel);
    }

    private void Separador_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_arrastrando)
        {
            return;
        }

        _arrastrando = false;
        if (sender is UIElement elemento)
        {
            elemento.ReleasePointerCapture(e.Pointer);
        }

        _alFinalizarArrastre?.Invoke();
    }

    /// <summary>Convierte filas en estrellas a píxeles según el layout actual.</summary>
    private void NormalizarFilasAPixeles()
    {
        var altoSuperior = ObtenerAltoFila(_indiceFilaSuperior);
        var altoInferior = ObtenerAltoFila(_indiceFilaInferior);

        _filaSuperior.Height = new GridLength(altoSuperior, GridUnitType.Pixel);
        _filaInferior.Height = new GridLength(altoInferior, GridUnitType.Pixel);
    }

    private double ObtenerAltoFila(int indiceFila)
    {
        var alto = 0d;
        foreach (var hijo in _panel.Children)
        {
            if (hijo is FrameworkElement elemento && Grid.GetRow(elemento) == indiceFila)
            {
                alto = Math.Max(alto, elemento.ActualHeight);
            }
        }

        return alto > 0 ? alto : _altoMinimo;
    }
}
