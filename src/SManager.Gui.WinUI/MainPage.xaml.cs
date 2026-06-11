using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using SManager.Gui.WinUI.Models;
using SManager.Gui.WinUI.Servicios;
using SManager.Gui.WinUI.ViewModels;

namespace SManager.Gui.WinUI;

public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; } = new();

    private bool _desplazarRegistroAlMostrar = true;

    /// <summary>Evita bucles al revertir el ComboBox de perfil tras cancelar un cambio.</summary>
    private bool _silenciandoCambioPerfil;

    /// <summary>Tras confirmar cierre con cambios pendientes, permite cerrar sin volver a preguntar.</summary>
    private bool _cierreVentanaAutorizado;

    private bool _preferenciasMonitorRestauradas;

    private bool _bandejaSuscrita;

    private bool _notificacionesSuscritas;

    private ArrastradorSeparadorMonitor? _arrastradorSeparadorSuperior;
    private ArrastradorSeparadorMonitor? _arrastradorSeparadorInferior;

    private ArrastradorSeparadorColumnas? _arrRegistroHoraPar;
    private ArrastradorSeparadorColumnas? _arrRegistroParNivel;
    private ArrastradorSeparadorColumnas? _arrRegistroNivelMensaje;
    private ArrastradorSeparadorColumnas? _arrMonitorNombreEstado;
    private ArrastradorSeparadorColumnas? _arrMonitorEstadoTamano;
    private ArrastradorSeparadorColumnas? _arrMonitorTamanoCopiados;
    private ArrastradorSeparadorColumnas? _arrMonitorCopiadosErrores;

    public MainPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        ControlAsistente.ViewModel = ViewModel.Asistente;
        ViewModel.RegistroDesplazarAlFinalSolicitado += ViewModel_RegistroDesplazarAlFinalSolicitado;
        ViewModel.ModoInterfazCambiado += ViewModel_ModoInterfazCambiado;
        Loaded += MainPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Inicializar();
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (NavPrincipal.MenuItems.Count > 0)
        {
            NavPrincipal.SelectedItem = NavPrincipal.MenuItems[0];
        }

        MostrarSeccion("inicio");
        App.Window.AppWindow.Closing += AppWindow_Closing;
        ConfigurarSeparadoresMonitor();
        ConfigurarColumnasRegistro();
        ConfigurarColumnasMonitor();
        SuscribirAccionesBandeja();
        SuscribirAccionesNotificacion();
    }

    /// <summary>Conecta el menú de la bandeja con la ventana y los comandos del ViewModel.</summary>
    private void SuscribirAccionesBandeja()
    {
        if (_bandejaSuscrita)
        {
            return;
        }

        ServicioAccionesBandeja.AbrirVentanaSolicitado += Bandeja_AbrirVentanaSolicitada;
        ServicioAccionesBandeja.IniciarSincronizacionSolicitado += Bandeja_IniciarSolicitado;
        ServicioAccionesBandeja.DetenerSincronizacionSolicitado += Bandeja_DetenerSolicitado;
        ServicioAccionesBandeja.VerMonitorSolicitado += Bandeja_VerMonitorSolicitado;
        ServicioAccionesBandeja.SalirAplicacionSolicitado += Bandeja_SalirSolicitado;
        _bandejaSuscrita = true;
    }

    /// <summary>Conecta los botones de los toasts con la ventana y la navegación.</summary>
    private void SuscribirAccionesNotificacion()
    {
        if (_notificacionesSuscritas)
        {
            return;
        }

        ServicioAccionesNotificacion.AbrirVentanaSolicitado += Notificacion_AbrirVentanaSolicitada;
        ServicioAccionesNotificacion.VerDetallesSolicitado += Notificacion_VerDetallesSolicitado;
        _notificacionesSuscritas = true;
        ServicioAccionesNotificacion.ReproducirAccionPendiente();
    }

    private void DesuscribirAccionesNotificacion()
    {
        if (!_notificacionesSuscritas)
        {
            return;
        }

        ServicioAccionesNotificacion.AbrirVentanaSolicitado -= Notificacion_AbrirVentanaSolicitada;
        ServicioAccionesNotificacion.VerDetallesSolicitado -= Notificacion_VerDetallesSolicitado;
        _notificacionesSuscritas = false;
    }

    private void Notificacion_AbrirVentanaSolicitada() =>
        EjecutarEnHiloUi(RestaurarVentanaDesdeBandeja);

    private void Notificacion_VerDetallesSolicitado(string seccion) =>
        EjecutarEnHiloUi(() =>
        {
            RestaurarVentanaDesdeBandeja();
            SeleccionarSeccionPorTag(NormalizarSeccionNotificacion(seccion));
        });

    private static string NormalizarSeccionNotificacion(string seccion) =>
        string.IsNullOrWhiteSpace(seccion) ? "registro" : seccion.Trim().ToLowerInvariant();

    private void DesuscribirAccionesBandeja()
    {
        if (!_bandejaSuscrita)
        {
            return;
        }

        ServicioAccionesBandeja.AbrirVentanaSolicitado -= Bandeja_AbrirVentanaSolicitada;
        ServicioAccionesBandeja.IniciarSincronizacionSolicitado -= Bandeja_IniciarSolicitado;
        ServicioAccionesBandeja.DetenerSincronizacionSolicitado -= Bandeja_DetenerSolicitado;
        ServicioAccionesBandeja.VerMonitorSolicitado -= Bandeja_VerMonitorSolicitado;
        ServicioAccionesBandeja.SalirAplicacionSolicitado -= Bandeja_SalirSolicitado;
        _bandejaSuscrita = false;
    }

    private void EjecutarEnHiloUi(Action accion) =>
        DispatcherQueue.TryEnqueue(() => accion());

    private void Bandeja_AbrirVentanaSolicitada() =>
        EjecutarEnHiloUi(RestaurarVentanaDesdeBandeja);

    private void Bandeja_IniciarSolicitado() =>
        EjecutarEnHiloUi(async () =>
        {
            if (ViewModel.PuedeIniciar)
            {
                await ViewModel.IniciarCommand.ExecuteAsync(null);
            }
        });

    private void Bandeja_DetenerSolicitado() =>
        EjecutarEnHiloUi(async () =>
        {
            if (ViewModel.PuedeDetener)
            {
                await ViewModel.DetenerCommand.ExecuteAsync(null);
            }
        });

    private void Bandeja_VerMonitorSolicitado() =>
        EjecutarEnHiloUi(() =>
        {
            RestaurarVentanaDesdeBandeja();
            SeleccionarSeccionPorTag("monitor");
        });

    private void Bandeja_SalirSolicitado() =>
        EjecutarEnHiloUi(async () => await CerrarAplicacionDesdeBandejaAsync());

    private void RestaurarVentanaDesdeBandeja()
    {
        if (App.Window is MainWindow ventana)
        {
            ventana.RestaurarDesdeBandeja();
        }
    }

    private async Task CerrarAplicacionDesdeBandejaAsync()
    {
        ViewModel.SolicitarSalirAplicacion();

        if (!_cierreVentanaAutorizado && ViewModel.HayCambiosSinGuardar)
        {
            RestaurarVentanaDesdeBandeja();
            var decision = await ViewModel.PreguntarCambiosSinGuardarAsync("cerrar la aplicación");
            if (decision == DecisionCambiosPendientes.Cancelar)
            {
                return;
            }

            if (decision == DecisionCambiosPendientes.GuardarYContinuar
                && ViewModel.GuardarCommand.CanExecute(null))
            {
                await ViewModel.GuardarCommand.ExecuteAsync(null);
            }
            else if (decision == DecisionCambiosPendientes.ContinuarSinGuardar)
            {
                ViewModel.DescartarCambiosPendientes();
            }
        }

        _cierreVentanaAutorizado = true;
        App.Window.AppWindow.Closing -= AppWindow_Closing;
        DesuscribirAccionesBandeja();
        DesuscribirAccionesNotificacion();
        ViewModel.Dispose();
        App.Window.Close();
    }

    /// <summary>Enlaza los separadores horizontales del monitor (sin dependencias externas).</summary>
    private void ConfigurarSeparadoresMonitor()
    {
        void GuardarPreferencias() =>
            ServicioPreferenciasMonitor.Guardar(
                FilaMonitorPares,
                FilaMonitorCopias,
                FilaMonitorActividad);

        _arrastradorSeparadorSuperior = new ArrastradorSeparadorMonitor(
            PanelMonitor,
            FilaMonitorPares,
            FilaMonitorCopias,
            indiceFilaSuperior: 1,
            indiceFilaInferior: 3,
            GuardarPreferencias);
        _arrastradorSeparadorSuperior.Enlazar(SeparadorMonitorSuperior);

        _arrastradorSeparadorInferior = new ArrastradorSeparadorMonitor(
            PanelMonitor,
            FilaMonitorCopias,
            FilaMonitorActividad,
            indiceFilaSuperior: 3,
            indiceFilaInferior: 5,
            GuardarPreferencias);
        _arrastradorSeparadorInferior.Enlazar(SeparadorMonitorInferior);
    }

    /// <summary>Enlaza separadores verticales de la cabecera del registro de actividad.</summary>
    private void ConfigurarColumnasRegistro()
    {
        void Guardar() => ViewModel.GuardarPreferenciasColumnas();

        _arrRegistroHoraPar = new ArrastradorSeparadorColumnas(
            CabeceraRegistro,
            ColRegistroHora,
            ColRegistroPar,
            (izq, der) =>
            {
                ViewModel.AnchoRegistroHora = izq;
                ViewModel.AnchoRegistroPar = der;
            },
            Guardar);
        _arrRegistroHoraPar.Enlazar(SepRegistroHoraPar);

        _arrRegistroParNivel = new ArrastradorSeparadorColumnas(
            CabeceraRegistro,
            ColRegistroPar,
            ColRegistroNivel,
            (izq, der) =>
            {
                ViewModel.AnchoRegistroPar = izq;
                ViewModel.AnchoRegistroNivel = der;
            },
            Guardar);
        _arrRegistroParNivel.Enlazar(SepRegistroParNivel);

        _arrRegistroNivelMensaje = new ArrastradorSeparadorColumnas(
            CabeceraRegistro,
            ColRegistroNivel,
            ColRegistroMensaje,
            (izq, der) =>
            {
                ViewModel.AnchoRegistroNivel = izq;
                ViewModel.AnchoRegistroMensaje = der;
            },
            Guardar);
        _arrRegistroNivelMensaje.Enlazar(SepRegistroNivelMensaje);
    }

    /// <summary>Enlaza separadores verticales de la tabla de pares del monitor.</summary>
    private void ConfigurarColumnasMonitor()
    {
        void Guardar() => ViewModel.GuardarPreferenciasColumnas();

        _arrMonitorNombreEstado = new ArrastradorSeparadorColumnas(
            CabeceraMonitorPares,
            ColMonitorNombre,
            ColMonitorEstado,
            (izq, der) =>
            {
                ViewModel.AnchoMonitorNombre = izq;
                ViewModel.AnchoMonitorEstado = der;
            },
            Guardar);
        _arrMonitorNombreEstado.Enlazar(SepMonitorNombreEstado);

        _arrMonitorEstadoTamano = new ArrastradorSeparadorColumnas(
            CabeceraMonitorPares,
            ColMonitorEstado,
            ColMonitorTamanoDestino,
            (izq, der) =>
            {
                ViewModel.AnchoMonitorEstado = izq;
                ViewModel.AnchoMonitorTamanoDestino = der;
            },
            Guardar);
        _arrMonitorEstadoTamano.Enlazar(SepMonitorEstadoTamano);

        _arrMonitorTamanoCopiados = new ArrastradorSeparadorColumnas(
            CabeceraMonitorPares,
            ColMonitorTamanoDestino,
            ColMonitorCopiados,
            (izq, der) =>
            {
                ViewModel.AnchoMonitorTamanoDestino = izq;
                ViewModel.AnchoMonitorCopiados = der;
            },
            Guardar);
        _arrMonitorTamanoCopiados.Enlazar(SepMonitorTamanoCopiados);

        _arrMonitorCopiadosErrores = new ArrastradorSeparadorColumnas(
            CabeceraMonitorPares,
            ColMonitorCopiados,
            ColMonitorErrores,
            (izq, der) =>
            {
                ViewModel.AnchoMonitorCopiados = izq;
                ViewModel.AnchoMonitorErrores = der;
            },
            Guardar);
        _arrMonitorCopiadosErrores.Enlazar(SepMonitorCopiadosErrores);
    }

    /// <summary>Captura el panel oculto de pares OK y lo guarda como PNG o JPG.</summary>
    private async void ExportarParesActivos_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.PrepararExportacionParesActivos();

        if (ViewModel.ParesExportacionImagen.Count == 0)
        {
            var aviso = new ContentDialog
            {
                Title = "Sin pares funcionando",
                Content = "No hay pares en estado OK con el demonio en ejecución. Inicia la sincronización y espera a que al menos un par esté activo.",
                CloseButtonText = "Entendido",
                XamlRoot = XamlRoot
            };
            await aviso.ShowAsync();
            return;
        }

        try
        {
            var guardado = await ServicioExportarImagen.ExportarAsync(
                PanelExportacionParesActivos,
                $"pares-funcionando-{DateTime.Now:yyyyMMdd-HHmm}");

            if (!guardado)
            {
                return;
            }

            var confirmacion = new ContentDialog
            {
                Title = "Imagen guardada",
                Content = "La captura se guardó correctamente.",
                CloseButtonText = "Entendido",
                XamlRoot = XamlRoot
            };
            await confirmacion.ShowAsync();
        }
        catch (Exception ex)
        {
            var error = new ContentDialog
            {
                Title = "No se pudo exportar",
                Content = ex.Message,
                CloseButtonText = "Entendido",
                XamlRoot = XamlRoot
            };
            await error.ShowAsync();
        }
    }

    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_cierreVentanaAutorizado)
        {
            return;
        }

        if (ViewModel.DebeOcultarEnBandejaAlCerrar())
        {
            args.Cancel = true;
            App.Window.AppWindow.Hide();
            ViewModel.NotificarOcultadoEnBandeja();
            return;
        }

        if (!ViewModel.HayCambiosSinGuardar)
        {
            return;
        }

        args.Cancel = true;

        var decision = await ViewModel.PreguntarCambiosSinGuardarAsync("cerrar la aplicación");
        if (decision == DecisionCambiosPendientes.Cancelar)
        {
            return;
        }

        if (decision == DecisionCambiosPendientes.GuardarYContinuar
            && ViewModel.GuardarCommand.CanExecute(null))
        {
            await ViewModel.GuardarCommand.ExecuteAsync(null);
        }
        else if (decision == DecisionCambiosPendientes.ContinuarSinGuardar)
        {
            ViewModel.DescartarCambiosPendientes();
        }

        _cierreVentanaAutorizado = true;
        App.Window.AppWindow.Closing -= AppWindow_Closing;
        DesuscribirAccionesBandeja();
        DesuscribirAccionesNotificacion();
        ViewModel.Dispose();
        App.Window.Close();
    }

    private void ViewModel_ModoInterfazCambiado(object? sender, EventArgs e)
    {
        if (ViewModel.ModoInterfazAvanzado)
        {
            return;
        }

        if (PanelMonitor.Visibility == Visibility.Visible
            || PanelRegistro.Visibility == Visibility.Visible
            || PanelEstadisticas.Visibility == Visibility.Visible)
        {
            SeleccionarSeccionPorTag("inicio");
        }
    }

    private void NavPrincipal_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            MostrarSeccion("ajustes");
            return;
        }

        if (args.SelectedItem is NavigationViewItem { Tag: string tag })
        {
            MostrarSeccion(tag);
        }
    }

    private void MostrarSeccion(string tag)
    {
        PanelInicio.Visibility = tag == "inicio" ? Visibility.Visible : Visibility.Collapsed;
        PanelPares.Visibility = tag == "pares" ? Visibility.Visible : Visibility.Collapsed;
        PanelMonitor.Visibility = tag == "monitor" ? Visibility.Visible : Visibility.Collapsed;
        PanelRegistro.Visibility = tag == "registro" ? Visibility.Visible : Visibility.Collapsed;
        PanelEstadisticas.Visibility = tag == "estadisticas" ? Visibility.Visible : Visibility.Collapsed;
        PanelGuia.Visibility = tag == "guia" ? Visibility.Visible : Visibility.Collapsed;
        PanelAjustes.Visibility = tag == "ajustes" ? Visibility.Visible : Visibility.Collapsed;

        if (tag == "monitor" && !_preferenciasMonitorRestauradas)
        {
            ServicioPreferenciasMonitor.RestaurarSiExiste(
                FilaMonitorPares,
                FilaMonitorCopias,
                FilaMonitorActividad);
            _preferenciasMonitorRestauradas = true;
        }

        if (tag == "registro" && _desplazarRegistroAlMostrar)
        {
            DesplazarRegistroAlFinal();
            _desplazarRegistroAlMostrar = false;
        }
    }

    private void ViewModel_RegistroDesplazarAlFinalSolicitado(object? sender, EventArgs e)
    {
        if (PanelRegistro.Visibility == Visibility.Visible)
        {
            DesplazarRegistroAlFinal();
        }
        else
        {
            _desplazarRegistroAlMostrar = true;
        }
    }

    /// <summary>Coloca el scroll en la última línea del registro.</summary>
    private void DesplazarRegistroAlFinal()
    {
        if (ViewModel.LineasRegistro.Count == 0)
        {
            return;
        }

        // Sin UpdateLayout(): forzar layout completo parpadea todas las filas visibles.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            var ultima = ViewModel.LineasRegistro[^1];
            ListaRegistro.ScrollIntoView(ultima);
        });
    }

    private void ConsejoIniciar_Cerrado(TeachingTip sender, object args) =>
        ViewModel.MarcarConsejoIniciarCerrado();

    private void ConsejoAnalizar_Cerrado(TeachingTip sender, object args) =>
        ViewModel.MarcarConsejoAnalizarCerrado();

    private void IrASincronizacion_Click(object sender, RoutedEventArgs e) =>
        SeleccionarSeccionPorTag("pares");

    private void IrAMonitor_Click(object sender, RoutedEventArgs e) =>
        SeleccionarSeccionPorTag("monitor");

    private void IrAGuia_Click(object sender, RoutedEventArgs e) =>
        SeleccionarSeccionPorTag("guia");

    private void IrAInicioAsistente_Click(object sender, RoutedEventArgs e)
    {
        SeleccionarSeccionPorTag("inicio");
        ViewModel.AbrirAsistenteOnboardingCommand.Execute(null);
    }

    private void RestablecerConsejos_Click(object sender, RoutedEventArgs e)
    {
        ServicioOnboarding.RestablecerConsejos();
        ViewModel.IntentarMostrarConsejosIniciales();
    }

    private void SeleccionarSeccionPorTag(string tag)
    {
        foreach (var item in NavPrincipal.MenuItems)
        {
            if (item is NavigationViewItem navItem && (string?)navItem.Tag == tag)
            {
                NavPrincipal.SelectedItem = navItem;
                MostrarSeccion(tag);
                return;
            }
        }
    }

    private async void Perfil_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_silenciandoCambioPerfil || e.AddedItems.Count == 0)
        {
            return;
        }

        if (ViewModel.ConsumirOmitirCambioPerfilInicial())
        {
            return;
        }

        var perfilNuevo = e.AddedItems[0] as string;
        var perfilAnterior = e.RemovedItems.Count > 0 ? e.RemovedItems[0] as string : null;
        if (string.IsNullOrWhiteSpace(perfilNuevo))
        {
            return;
        }

        var decision = await ViewModel.PreguntarCambiosSinGuardarAsync("cambiar de perfil");
        switch (decision)
        {
            case DecisionCambiosPendientes.Cancelar:
                if (!string.IsNullOrWhiteSpace(perfilAnterior))
                {
                    _silenciandoCambioPerfil = true;
                    ViewModel.PerfilSeleccionado = perfilAnterior;
                    _silenciandoCambioPerfil = false;
                }

                return;
            case DecisionCambiosPendientes.GuardarYContinuar:
                if (ViewModel.GuardarCommand.CanExecute(null))
                {
                    await ViewModel.GuardarCommand.ExecuteAsync(null);
                }

                break;
            case DecisionCambiosPendientes.ContinuarSinGuardar:
                ViewModel.DescartarCambiosPendientes();
                break;
        }

        ViewModel.AplicarCambioPerfilCommand.Execute(null);
    }

    private async void BotonIniciar_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.PuedeIniciar)
        {
            await ViewModel.IniciarCommand.ExecuteAsync(null);
        }
    }

    private async void BotonDetener_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.PuedeDetener)
        {
            await ViewModel.DetenerCommand.ExecuteAsync(null);
        }
    }

    private async void BotonRecargar_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.PuedeDetener)
        {
            await ViewModel.RecargarCommand.ExecuteAsync(null);
        }
    }

    /// <summary>Atajo Ctrl+S: guarda la configuración del perfil activo.</summary>
    private void AtajoGuardar_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel.GuardarCommand.CanExecute(null))
        {
            ViewModel.GuardarCommand.Execute(null);
            args.Handled = true;
        }
    }

    /// <summary>Atajo F5: recarga el demonio si está en ejecución.</summary>
    private async void AtajoRecargar_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel.PuedeDetener)
        {
            await ViewModel.RecargarCommand.ExecuteAsync(null);
            args.Handled = true;
        }
    }

    /// <summary>Atajo Ctrl+Shift+A: analizar cambios sin copiar.</summary>
    private async void AtajoAnalizar_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel.AnalizarCambiosCommand.CanExecute(null))
        {
            await ViewModel.AnalizarCambiosCommand.ExecuteAsync(null);
            args.Handled = true;
        }
    }

    /// <summary>Atajo Ctrl+I: inicia el demonio si está detenido.</summary>
    private async void AtajoIniciar_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel.PuedeIniciar)
        {
            await ViewModel.IniciarCommand.ExecuteAsync(null);
            args.Handled = true;
        }
    }

    /// <summary>Atajo Ctrl+Shift+S: detiene el demonio si está activo.</summary>
    private async void AtajoDetener_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel.PuedeDetener)
        {
            await ViewModel.DetenerCommand.ExecuteAsync(null);
            args.Handled = true;
        }
    }
}
