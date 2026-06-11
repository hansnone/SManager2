using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SManager.Core.Modelos;
using SManager.Gui.Shared.Modelos;
using SManager.Gui.Shared.Servicios;
using SManager.Gui.WinUI.Models;
using SManager.Gui.WinUI.Servicios;

namespace SManager.Gui.WinUI.ViewModels;

/// <summary>Pasos del asistente de configuración del primer par.</summary>
public enum PasoAsistenteOnboarding
{
    Bienvenida = 0,
    Plantilla = 1,
    Origen = 2,
    Destino = 3,
    Comportamiento = 4,
    VistaPrevia = 5,
    Finalizar = 6
}

/// <summary>Estado y comandos del asistente paso a paso (Fase 2 onboarding).</summary>
public partial class AsistenteOnboardingViewModel : ObservableObject
{
    public IReadOnlyList<PlantillaParEjemplo> Plantillas { get; } = CatalogoPlantillasPar.ObtenerPlantillas();

    [ObservableProperty]
    private PasoAsistenteOnboarding _pasoActual = PasoAsistenteOnboarding.Bienvenida;

    [ObservableProperty]
    private PlantillaParEjemplo? _plantillaSeleccionada;

    [ObservableProperty]
    private string _nombrePar = "Mi sincronización";

    [ObservableProperty]
    private string _rutaOrigen = string.Empty;

    [ObservableProperty]
    private string _rutaDestino = string.Empty;

    [ObservableProperty]
    private string _filtroInclusion = "*";

    [ObservableProperty]
    private string _filtroExclusion = "~$*;*.tmp;*.partial;*.lnk";

    [ObservableProperty]
    private bool _excluirTemporales = true;

    [ObservableProperty]
    private bool _iniciarAlFinalizar = true;

    [ObservableProperty]
    private bool _analizandoVistaPrevia;

    [ObservableProperty]
    private string _textoVistaPrevia = "Pulsa «Analizar ahora» para ver qué archivos se copiarían.";

    [ObservableProperty]
    private string _textoAvisosRiesgo = string.Empty;

    public string TituloPaso => PasoActual switch
    {
        PasoAsistenteOnboarding.Bienvenida => "Bienvenido a SManager 2.0",
        PasoAsistenteOnboarding.Plantilla => "Elige un ejemplo",
        PasoAsistenteOnboarding.Origen => "Carpeta origen",
        PasoAsistenteOnboarding.Destino => "Carpeta destino",
        PasoAsistenteOnboarding.Comportamiento => "Comportamiento básico",
        PasoAsistenteOnboarding.VistaPrevia => "Vista previa",
        PasoAsistenteOnboarding.Finalizar => "Activar sincronización",
        _ => "Asistente"
    };

    public string SubtituloPaso => PasoActual switch
    {
        PasoAsistenteOnboarding.Bienvenida =>
            "SManager copia archivos en una sola dirección: desde una carpeta origen hacia una carpeta destino. "
            + "No borra en destino lo que desaparezca del origen.",
        PasoAsistenteOnboarding.Plantilla =>
            "Selecciona el caso que más se parezca a lo que necesitas. Podrás cambiar todo después.",
        PasoAsistenteOnboarding.Origen =>
            "Elige la carpeta que quieres vigilar. SManager copiará desde aquí hacia el destino.",
        PasoAsistenteOnboarding.Destino =>
            "Elige dónde quieres la copia. Si ya tiene archivos, los del mismo nombre pueden sobrescribirse.",
        PasoAsistenteOnboarding.Comportamiento =>
            "Opciones sencillas para empezar. Los filtros avanzados están en Sincronización → Editar par.",
        PasoAsistenteOnboarding.VistaPrevia =>
            "Comprueba cuántos archivos se copiarían sin modificar nada todavía.",
        PasoAsistenteOnboarding.Finalizar =>
            "Revisa el resumen y crea tu primer par de sincronización.",
        _ => string.Empty
    };

    public bool PuedeRetroceder => PasoActual > PasoAsistenteOnboarding.Bienvenida;

    public bool EsUltimoPaso => PasoActual == PasoAsistenteOnboarding.Finalizar;

    public int IndicadorPaso => (int)PasoActual + 1;

    public int TotalPasos => 7;

    public string TextoIndicadorPaso => $"Paso {IndicadorPaso} de {TotalPasos}";

    public string TextoBotonPrincipal => EsUltimoPaso ? "Crear par" : "Siguiente";

    /// <summary>InfoBar.IsOpen exige bool, no Visibility.</summary>
    public bool MostrarAvisosRiesgo => !string.IsNullOrWhiteSpace(TextoAvisosRiesgo);

    /// <summary>El ViewModel principal se suscribe para aplicar el resultado.</summary>
    public event Func<ResultadoAsistenteOnboarding, Task>? FinalizadoSolicitado;

    public event EventHandler? CerrarSolicitado;

    public AsistenteOnboardingViewModel()
    {
        PlantillaSeleccionada = Plantillas.FirstOrDefault();
        AplicarPlantilla(PlantillaSeleccionada);
    }

    partial void OnPasoActualChanged(PasoAsistenteOnboarding value)
    {
        OnPropertyChanged(nameof(TituloPaso));
        OnPropertyChanged(nameof(SubtituloPaso));
        OnPropertyChanged(nameof(PuedeRetroceder));
        OnPropertyChanged(nameof(EsUltimoPaso));
        OnPropertyChanged(nameof(IndicadorPaso));
        OnPropertyChanged(nameof(TextoIndicadorPaso));
        OnPropertyChanged(nameof(TextoBotonPrincipal));

        if (value == PasoAsistenteOnboarding.Destino)
        {
            ActualizarAvisosRiesgo();
        }
    }

    partial void OnTextoAvisosRiesgoChanged(string value) =>
        OnPropertyChanged(nameof(MostrarAvisosRiesgo));

    partial void OnPlantillaSeleccionadaChanged(PlantillaParEjemplo? value) => AplicarPlantilla(value);

    partial void OnRutaOrigenChanged(string value) => ActualizarAvisosRiesgo();

    partial void OnRutaDestinoChanged(string value) => ActualizarAvisosRiesgo();

    partial void OnExcluirTemporalesChanged(bool value)
    {
        if (value)
        {
            FiltroExclusion = "~$*;*.tmp;*.partial;*.lnk;Thumbs.db;desktop.ini";
        }
    }

    [RelayCommand]
    private void SeleccionarPlantilla(PlantillaParEjemplo? plantilla)
    {
        if (plantilla is null)
        {
            return;
        }

        PlantillaSeleccionada = plantilla;
    }

    [RelayCommand]
    private void Anterior()
    {
        if (!PuedeRetroceder)
        {
            return;
        }

        PasoActual = (PasoAsistenteOnboarding)((int)PasoActual - 1);
    }

    [RelayCommand]
    private async Task SiguienteAsync()
    {
        if (!ValidarPasoActual(out var mensajeError))
        {
            if (!string.IsNullOrWhiteSpace(mensajeError))
            {
                await MostrarAvisoValidacionAsync(mensajeError);
            }

            return;
        }

        if (EsUltimoPaso)
        {
            await FinalizarAsync();
            return;
        }

        PasoActual = (PasoAsistenteOnboarding)((int)PasoActual + 1);

        if (PasoActual == PasoAsistenteOnboarding.VistaPrevia)
        {
            await AnalizarVistaPreviaAsync();
        }
    }

    [RelayCommand]
    private void Omitir()
    {
        ServicioOnboarding.MarcarAsistenteCompletado();
        CerrarSolicitado?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ElegirOrigenAsync()
    {
        var ruta = await ServicioSelectorCarpeta.ElegirCarpetaAsync(RutaOrigen);
        if (!string.IsNullOrWhiteSpace(ruta))
        {
            RutaOrigen = ruta;
        }
    }

    [RelayCommand]
    private async Task ElegirDestinoAsync()
    {
        var ruta = await ServicioSelectorCarpeta.ElegirCarpetaAsync(RutaDestino);
        if (!string.IsNullOrWhiteSpace(ruta))
        {
            RutaDestino = ruta;
        }
    }

    [RelayCommand]
    private async Task AnalizarVistaPreviaAsync()
    {
        if (AnalizandoVistaPrevia)
        {
            return;
        }

        AnalizandoVistaPrevia = true;
        try
        {
            var par = ConstruirParTemporal();
            var resultado = await Task.Run(() =>
                ServicioAnalisisCambios.AnalizarPar(par, CancellationToken.None));

            TextoVistaPrevia =
                $"Nuevos: {resultado.ArchivosNuevos:N0} ({ServicioFormateoEstadisticas.FormatearBytes(resultado.BytesNuevos)})\n"
                + $"Modificados: {resultado.ArchivosModificados:N0} ({ServicioFormateoEstadisticas.FormatearBytes(resultado.BytesModificados)})\n"
                + $"Omitidos por filtro: {resultado.OmitidosPorFiltro:N0}\n"
                + $"Ya sincronizados: {resultado.YaSincronizados:N0}\n"
                + $"Errores de acceso: {resultado.ErroresAcceso:N0}\n"
                + $"Total pendiente: {resultado.TotalPendientes:N0} — {ServicioFormateoEstadisticas.FormatearBytes(resultado.BytesPendientes)}";
        }
        catch (Exception ex)
        {
            TextoVistaPrevia = $"No se pudo analizar: {ex.Message}";
        }
        finally
        {
            AnalizandoVistaPrevia = false;
        }
    }

    private async Task FinalizarAsync()
    {
        if (!ValidarPasoActual(out var mensajeError))
        {
            if (!string.IsNullOrWhiteSpace(mensajeError))
            {
                await MostrarAvisoValidacionAsync(mensajeError);
            }

            return;
        }

        var par = new ParFilaViewModel
        {
            Nombre = NombrePar.Trim(),
            RutaOrigen = RutaOrigen.Trim(),
            RutaDestino = RutaDestino.Trim(),
            FiltroInclusion = string.IsNullOrWhiteSpace(FiltroInclusion) ? "*" : FiltroInclusion.Trim(),
            FiltroExclusion = FiltroExclusion.Trim(),
            Habilitado = true,
            Pausado = false
        };
        par.ActualizarAvisosRiesgo();

        var resultado = new ResultadoAsistenteOnboarding
        {
            Par = par,
            GuardarConfiguracion = true,
            IniciarSincronizacion = IniciarAlFinalizar,
            MarcarAsistenteCompletado = true
        };

        if (FinalizadoSolicitado is not null)
        {
            await FinalizadoSolicitado.Invoke(resultado);
        }
    }

    private bool ValidarPasoActual(out string? mensajeError)
    {
        mensajeError = PasoActual switch
        {
            PasoAsistenteOnboarding.Plantilla when PlantillaSeleccionada is null =>
                "Selecciona una plantilla para continuar.",
            PasoAsistenteOnboarding.Origen when string.IsNullOrWhiteSpace(RutaOrigen) =>
                "Elige la carpeta origen antes de continuar.",
            PasoAsistenteOnboarding.Origen when !Directory.Exists(RutaOrigen) =>
                "La carpeta origen no existe o no está accesible.",
            PasoAsistenteOnboarding.Destino when string.IsNullOrWhiteSpace(RutaDestino) =>
                "Elige la carpeta destino antes de continuar.",
            PasoAsistenteOnboarding.Destino when !Directory.Exists(RutaDestino) =>
                "La carpeta destino no existe o no está accesible.",
            PasoAsistenteOnboarding.Finalizar when string.IsNullOrWhiteSpace(NombrePar) =>
                "Indica un nombre para el par.",
            _ => null
        };

        return mensajeError is null;
    }

    private static async Task MostrarAvisoValidacionAsync(string mensaje)
    {
        var cuadro = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "Revisa este paso",
            Content = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = mensaje,
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords
            },
            CloseButtonText = "Entendido",
            XamlRoot = App.Window.Content.XamlRoot
        };
        await cuadro.ShowAsync();
    }

    private void AplicarPlantilla(PlantillaParEjemplo? plantilla)
    {
        if (plantilla is null)
        {
            return;
        }

        NombrePar = plantilla.NombreParSugerido;
        FiltroInclusion = plantilla.FiltroInclusion;
        FiltroExclusion = plantilla.FiltroExclusion;
        ExcluirTemporales = true;
    }

    private void ActualizarAvisosRiesgo()
    {
        if (string.IsNullOrWhiteSpace(RutaOrigen) && string.IsNullOrWhiteSpace(RutaDestino))
        {
            TextoAvisosRiesgo = string.Empty;
            return;
        }

        var avisos = ServicioValidacionRiesgoPar.DetectarAvisos(ConstruirParTemporal());
        TextoAvisosRiesgo = avisos.Count == 0
            ? string.Empty
            : string.Join("\n", avisos.Select(a => $"• {a}"));
    }

    private ParSincronizacion ConstruirParTemporal() => new()
    {
        Nombre = NombrePar,
        RutaOrigen = RutaOrigen,
        RutaDestino = RutaDestino,
        FiltroInclusion = FiltroInclusion,
        FiltroExclusion = FiltroExclusion,
        Habilitado = true
    };

    public string ResumenFinal =>
        $"Par: {NombrePar}\n"
        + $"Origen: {RutaOrigen}\n"
        + $"Destino: {RutaDestino}\n"
        + $"Incluir: {FiltroInclusion}\n"
        + $"Excluir: {FiltroExclusion}";
}
