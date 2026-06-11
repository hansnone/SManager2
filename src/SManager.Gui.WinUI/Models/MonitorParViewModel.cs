using CommunityToolkit.Mvvm.ComponentModel;

namespace SManager.Gui.WinUI.Models;

/// <summary>Fila del monitor en tiempo real con chip de estado tematizado en XAML.</summary>
public partial class MonitorParViewModel : ObservableObject
{
    [ObservableProperty]
    private string _nombre = string.Empty;

    [ObservableProperty]
    private string _estado = string.Empty;

    [ObservableProperty]
    private string _rutaDestino = string.Empty;

    [ObservableProperty]
    private string _tamanoDestinoTexto = "—";

    [ObservableProperty]
    private int _copiados;

    [ObservableProperty]
    private int _errores;

    [ObservableProperty]
    private bool _estaFuncionando;

    /// <summary>Actualiza solo propiedades distintas para evitar repintado completo de la fila.</summary>
    public void ActualizarDesde(
        string nombre,
        string estado,
        string rutaDestino,
        string tamanoDestinoTexto,
        int copiados,
        int errores,
        bool estaFuncionando)
    {
        if (Nombre != nombre)
        {
            Nombre = nombre;
        }

        if (Estado != estado)
        {
            Estado = estado;
        }

        if (RutaDestino != rutaDestino)
        {
            RutaDestino = rutaDestino;
        }

        if (TamanoDestinoTexto != tamanoDestinoTexto)
        {
            TamanoDestinoTexto = tamanoDestinoTexto;
        }

        if (Copiados != copiados)
        {
            Copiados = copiados;
        }

        if (Errores != errores)
        {
            Errores = errores;
        }

        if (EstaFuncionando != estaFuncionando)
        {
            EstaFuncionando = estaFuncionando;
        }
    }

    public static MonitorParViewModel Crear(
        string nombre,
        string estado,
        string rutaDestino,
        string tamanoDestinoTexto,
        int copiados,
        int errores,
        bool estaFuncionando) =>
        new()
        {
            Nombre = nombre,
            Estado = estado,
            RutaDestino = rutaDestino,
            TamanoDestinoTexto = tamanoDestinoTexto,
            Copiados = copiados,
            Errores = errores,
            EstaFuncionando = estaFuncionando
        };
}
