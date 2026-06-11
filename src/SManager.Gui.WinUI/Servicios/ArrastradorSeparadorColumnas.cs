using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>
/// Redimensiona dos columnas adyacentes de un Grid arrastrando un separador vertical.
/// Mismo enfoque que <see cref="ArrastradorSeparadorMonitor"/> (sin GridSplitter externo).
/// </summary>
public sealed class ArrastradorSeparadorColumnas
{
    private readonly Grid _cuadricula;
    private readonly ColumnDefinition _columnaIzquierda;
    private readonly ColumnDefinition _columnaDerecha;
    private readonly Action<double, double>? _alCambiarAnchos;
    private readonly Action? _alFinalizarArrastre;
    private readonly double _anchoMinimo;

    private bool _arrastrando;
    private double _ultimaPosicionX;
    private double _anchoIzquierdoInicial;
    private double _anchoDerechoInicial;

    public ArrastradorSeparadorColumnas(
        Grid cuadricula,
        ColumnDefinition columnaIzquierda,
        ColumnDefinition columnaDerecha,
        Action<double, double>? alCambiarAnchos = null,
        Action? alFinalizarArrastre = null,
        double anchoMinimo = 48)
    {
        _cuadricula = cuadricula;
        _columnaIzquierda = columnaIzquierda;
        _columnaDerecha = columnaDerecha;
        _alCambiarAnchos = alCambiarAnchos;
        _alFinalizarArrastre = alFinalizarArrastre;
        _anchoMinimo = anchoMinimo;
    }

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
        _ultimaPosicionX = e.GetCurrentPoint(_cuadricula).Position.X;

        NormalizarColumnasAPixeles();
        _anchoIzquierdoInicial = _columnaIzquierda.Width.Value;
        _anchoDerechoInicial = _columnaDerecha.Width.Value;
    }

    private void Separador_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_arrastrando)
        {
            return;
        }

        var posicionX = e.GetCurrentPoint(_cuadricula).Position.X;
        var delta = posicionX - _ultimaPosicionX;
        if (Math.Abs(delta) < 0.5)
        {
            return;
        }

        var nuevoIzquierdo = Math.Max(_anchoMinimo, _anchoIzquierdoInicial + delta);
        var nuevoDerecho = Math.Max(_anchoMinimo, _anchoDerechoInicial - delta);

        if (nuevoIzquierdo <= _anchoMinimo || nuevoDerecho <= _anchoMinimo)
        {
            return;
        }

        _columnaIzquierda.Width = new GridLength(nuevoIzquierdo, GridUnitType.Pixel);
        _columnaDerecha.Width = new GridLength(nuevoDerecho, GridUnitType.Pixel);
        _alCambiarAnchos?.Invoke(nuevoIzquierdo, nuevoDerecho);
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

    private void NormalizarColumnasAPixeles()
    {
        var anchoIzquierdo = ObtenerAnchoColumna(_columnaIzquierda);
        var anchoDerecho = ObtenerAnchoColumna(_columnaDerecha);

        _columnaIzquierda.Width = new GridLength(anchoIzquierdo, GridUnitType.Pixel);
        _columnaDerecha.Width = new GridLength(anchoDerecho, GridUnitType.Pixel);
    }

    private double ObtenerAnchoColumna(ColumnDefinition columna)
    {
        if (columna.Width.GridUnitType == GridUnitType.Pixel && columna.Width.Value > 0)
        {
            return columna.Width.Value;
        }

        var indice = _cuadricula.ColumnDefinitions.IndexOf(columna);
        if (indice < 0)
        {
            return _anchoMinimo;
        }

        var ancho = 0d;
        foreach (var hijo in _cuadricula.Children)
        {
            if (hijo is FrameworkElement elemento && Grid.GetColumn(elemento) == indice)
            {
                ancho = Math.Max(ancho, elemento.ActualWidth);
            }
        }

        return ancho > 0 ? ancho : _anchoMinimo;
    }
}
