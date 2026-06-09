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
    private int _copiados;

    [ObservableProperty]
    private int _errores;

    /// <summary>Actualiza solo propiedades distintas para evitar repintado completo de la fila.</summary>
    public void ActualizarDesde(string nombre, string estado, int copiados, int errores)
    {
        if (Nombre != nombre)
        {
            Nombre = nombre;
        }

        if (Estado != estado)
        {
            Estado = estado;
        }

        if (Copiados != copiados)
        {
            Copiados = copiados;
        }

        if (Errores != errores)
        {
            Errores = errores;
        }
    }

    public static MonitorParViewModel Crear(string nombre, string estado, int copiados, int errores) =>
        new()
        {
            Nombre = nombre,
            Estado = estado,
            Copiados = copiados,
            Errores = errores
        };
}
