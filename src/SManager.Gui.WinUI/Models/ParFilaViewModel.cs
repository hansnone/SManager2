using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SManager.Gui.WinUI.Servicios;

namespace SManager.Gui.WinUI.Models;

/// <summary>Fila editable del grid de pares en la GUI.</summary>
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

    public int TotalCopiados { get; set; }

    public int TotalErrores { get; set; }

    [RelayCommand]
    private async Task ExaminarOrigenAsync()
    {
        var ruta = await ServicioSelectorCarpeta.ElegirCarpetaAsync(RutaOrigen);
        if (!string.IsNullOrEmpty(ruta))
        {
            RutaOrigen = ruta;
        }
    }

    [RelayCommand]
    private async Task ExaminarDestinoAsync()
    {
        var ruta = await ServicioSelectorCarpeta.ElegirCarpetaAsync(RutaDestino);
        if (!string.IsNullOrEmpty(ruta))
        {
            RutaDestino = ruta;
        }
    }
}
