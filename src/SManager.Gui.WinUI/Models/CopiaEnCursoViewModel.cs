using CommunityToolkit.Mvvm.ComponentModel;

namespace SManager.Gui.WinUI.Models;

/// <summary>Copia activa en el monitor con progreso y ETA estimada.</summary>
public partial class CopiaEnCursoViewModel : ObservableObject
{
    [ObservableProperty]
    private int _copiador;

    [ObservableProperty]
    private string _archivo = string.Empty;

    [ObservableProperty]
    private string _idPar = string.Empty;

    [ObservableProperty]
    private int _porcentaje;

    [ObservableProperty]
    private int? _etaSegundos;

    [ObservableProperty]
    private long _bytesTotales;

    [ObservableProperty]
    private bool _mostrarBarraProgreso;

    [ObservableProperty]
    private double _valorProgreso;

    [ObservableProperty]
    private string _textoEta = "—";

    [ObservableProperty]
    private string _textoProgreso = "—";

    /// <summary>Sincroniza telemetría IPC sin recrear el objeto de fila.</summary>
    public void ActualizarDesde(
        int copiador,
        string archivo,
        string idPar,
        int porcentaje,
        int? etaSegundos,
        long bytesTotales)
    {
        if (Copiador != copiador)
        {
            Copiador = copiador;
        }

        if (Archivo != archivo)
        {
            Archivo = archivo;
        }

        if (IdPar != idPar)
        {
            IdPar = idPar;
        }

        if (Porcentaje != porcentaje)
        {
            Porcentaje = porcentaje;
        }

        if (EtaSegundos != etaSegundos)
        {
            EtaSegundos = etaSegundos;
        }

        if (BytesTotales != bytesTotales)
        {
            BytesTotales = bytesTotales;
        }

        var mostrarBarra = bytesTotales > 0 && porcentaje < 100;
        if (MostrarBarraProgreso != mostrarBarra)
        {
            MostrarBarraProgreso = mostrarBarra;
        }

        var valorProgreso = (double)porcentaje;
        if (Math.Abs(ValorProgreso - valorProgreso) > 0.01)
        {
            ValorProgreso = valorProgreso;
        }

        var textoProgreso = bytesTotales > 0 ? $"{porcentaje}%" : "—";
        if (TextoProgreso != textoProgreso)
        {
            TextoProgreso = textoProgreso;
        }

        var textoEta = FormatearEta(porcentaje, etaSegundos);
        if (TextoEta != textoEta)
        {
            TextoEta = textoEta;
        }
    }

    public static CopiaEnCursoViewModel Crear(
        int copiador,
        string archivo,
        string idPar,
        int porcentaje,
        int? etaSegundos,
        long bytesTotales)
    {
        var vm = new CopiaEnCursoViewModel();
        vm.ActualizarDesde(copiador, archivo, idPar, porcentaje, etaSegundos, bytesTotales);
        return vm;
    }

    private static string FormatearEta(int porcentaje, int? etaSegundos)
    {
        if (etaSegundos is int eta && eta > 0)
        {
            return $"~{eta}s";
        }

        if (porcentaje >= 100)
        {
            return "…";
        }

        return "—";
    }
}
