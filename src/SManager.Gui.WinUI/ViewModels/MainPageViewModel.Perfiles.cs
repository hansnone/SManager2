using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SManager.Core.Modelos;
using SManager.Gui.Shared;
using SManager.Gui.WinUI.Models;
using SManager.Gui.WinUI.Servicios;
using SManager.Ipc;

namespace SManager.Gui.WinUI.ViewModels;

/// <summary>
/// Parte del ViewModel dedicada a la gestión de perfiles, persistencia de configuración JSON
/// y configuración del auto-inicio con Windows.
/// </summary>
public partial class MainPageViewModel
{
    partial void OnAutoInicioHabilitadoChanged(bool value)
    {
        if (_cargandoPreferenciasAutoInicio)
        {
            return;
        }

        PersistirPreferenciasAutoInicio();
    }

    partial void OnAutoInicioMinimizadoChanged(bool value)
    {
        if (_cargandoPreferenciasAutoInicio)
        {
            return;
        }

        if (value && !BandejaHabilitada)
        {
            BandejaHabilitada = true;
        }

        PersistirPreferenciasAutoInicio();
    }

    partial void OnAutoInicioIniciarDemonioChanged(bool value)
    {
        if (_cargandoPreferenciasAutoInicio)
        {
            return;
        }

        PersistirPreferenciasAutoInicio();
    }

    private void CargarPreferenciasAutoInicio()
    {
        _cargandoPreferenciasAutoInicio = true;
        try
        {
            var preferencias = ServicioPreferenciasGui.Cargar();
            var registro = ServicioAutoInicioSistema.LeerEstado();
            var archivoExiste = File.Exists(
                Path.Combine(RutasDatos.ResolverRaiz(), "preferencias_gui.json"));

            if (!archivoExiste && registro.Habilitado)
            {
                AutoInicioHabilitado = true;
                AutoInicioMinimizado = registro.Minimizado;
                AutoInicioIniciarDemonio = registro.IniciarDemonio;
                ServicioPreferenciasGui.Guardar(new PreferenciasGuiDto
                {
                    AutoInicioHabilitado = AutoInicioHabilitado,
                    AutoInicioMinimizado = AutoInicioMinimizado,
                    AutoInicioIniciarDemonio = AutoInicioIniciarDemonio
                });
                return;
            }

            AutoInicioHabilitado = preferencias.AutoInicioHabilitado;
            AutoInicioMinimizado = preferencias.AutoInicioMinimizado;
            AutoInicioIniciarDemonio = preferencias.AutoInicioIniciarDemonio;

            if (AutoInicioHabilitado != registro.Habilitado
                || (AutoInicioHabilitado && AutoInicioMinimizado != registro.Minimizado)
                || (AutoInicioHabilitado && AutoInicioIniciarDemonio != registro.IniciarDemonio))
            {
                ServicioAutoInicioSistema.Aplicar(
                    AutoInicioHabilitado,
                    AutoInicioMinimizado,
                    AutoInicioIniciarDemonio);
            }
        }
        finally
        {
            _cargandoPreferenciasAutoInicio = false;
        }
    }

    private void PersistirPreferenciasAutoInicio()
    {
        var preferencias = ServicioPreferenciasGui.Cargar();
        preferencias.AutoInicioHabilitado = AutoInicioHabilitado;
        preferencias.AutoInicioMinimizado = AutoInicioMinimizado;
        preferencias.AutoInicioIniciarDemonio = AutoInicioIniciarDemonio;
        ServicioPreferenciasGui.Guardar(preferencias);

        try
        {
            ServicioAutoInicioSistema.Aplicar(
                AutoInicioHabilitado,
                AutoInicioMinimizado,
                AutoInicioIniciarDemonio);
        }
        catch (Exception ex)
        {
            TextoEstado = $"No se pudo actualizar el inicio automático: {ex.Message}";
        }
    }

    /// <summary>Carga el perfil ya seleccionado en el desplegable lateral.</summary>
    [RelayCommand]
    private void AplicarCambioPerfil()
    {
        CargarConfiguracionPerfilActual();
        ActualizarEstadoBotones();
        _monitorNotificaciones.Reiniciar();
        _demonioEstabaEnEjecucion = DemonioEnEjecucion;
        _datosSesionActual = null;
        ReiniciarRegistroPerfil();
        Estadisticas.ReiniciarMuestreo();
        LimpiarVistaTelemetria();
        CargarHistorialSesiones(PerfilActual());
    }

    /// <summary>Pregunta qué hacer con cambios sin guardar antes de cambiar de perfil o cerrar la aplicación.</summary>
    public async Task<DecisionCambiosPendientes> PreguntarCambiosSinGuardarAsync(string motivo)
    {
        if (!HayCambiosSinGuardar)
        {
            return DecisionCambiosPendientes.ContinuarSinGuardar;
        }

        var cuadro = new ContentDialog
        {
            Title = "Cambios sin guardar",
            Content = new TextBlock
            {
                Text =
                    $"Hay cambios en el perfil «{PerfilActual()}» que no se han guardado en disco.\n\n"
                    + $"¿Qué deseas hacer al {motivo}?",
                TextWrapping = TextWrapping.WrapWholeWords
            },
            PrimaryButtonText = "Guardar",
            SecondaryButtonText = "Descartar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.Window.Content.XamlRoot
        };

        var resultado = await cuadro.ShowAsync();
        return resultado switch
        {
            ContentDialogResult.Primary => DecisionCambiosPendientes.GuardarYContinuar,
            ContentDialogResult.Secondary => DecisionCambiosPendientes.ContinuarSinGuardar,
            _ => DecisionCambiosPendientes.Cancelar
        };
    }

    private void MarcarComoSucio()
    {
        if (_suprimirMarcadoSucio)
        {
            return;
        }

        HayCambiosSinGuardar = true;
    }

    private void MarcarComoGuardado()
    {
        _suprimirMarcadoSucio = true;
        HayCambiosSinGuardar = false;
        _suprimirMarcadoSucio = false;
    }

    /// <summary>Indica que los cambios en memoria se descartan.</summary>
    public void DescartarCambiosPendientes() => MarcarComoGuardado();

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private async Task EliminarPerfilAsync()
    {
        var perfil = PerfilActual();
        if (_ipc.EstaDemonioEnEjecucion(perfil))
        {
            await MostrarAvisoAsync("Detén el demonio antes de eliminar este perfil.", "Demonio activo");
            return;
        }

        _temporizador?.Stop();

        var usaPersonalizada = _servicioConfig.UsaRutaPersonalizada(perfil);
        var rutaPersonalizada = usaPersonalizada
            ? _servicioConfig.ResolverRutaConfiguracion(perfil)
            : null;

        var casillaBorrarJson = new CheckBox
        {
            Content = "También borrar el archivo JSON personalizado del disco",
            IsEnabled = usaPersonalizada,
            IsChecked = false,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var textoAviso = usaPersonalizada
            ? $"Se eliminará el perfil «{perfil}» y sus datos en %LOCALAPPDATA%\\SManager2.\n\n"
              + $"Por defecto se conserva el JSON en:\n{rutaPersonalizada}"
            : $"Se eliminará el perfil «{perfil}» y sus datos en %LOCALAPPDATA%\\SManager2 "
              + "(configuración, log, telemetría IPC).\n\n"
              + "No se borran archivos ya copiados en destino.";

        var cuadro = new ContentDialog
        {
            Title = "Eliminar perfil",
            PrimaryButtonText = "Eliminar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.Window.Content.XamlRoot,
            Content = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = textoAviso,
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    casillaBorrarJson
                }
            }
        };

        if (await cuadro.ShowAsync() != ContentDialogResult.Primary)
        {
            _temporizador?.Start();
            return;
        }

        var resultado = ServicioEliminacionPerfil.Eliminar(
            perfil,
            _ipc,
            eliminarJsonPersonalizado: casillaBorrarJson.IsChecked == true);

        if (!resultado.Exito)
        {
            _temporizador?.Start();
            await MostrarAvisoAsync(resultado.MensajeError ?? "No se pudo eliminar el perfil.", "Error");
            return;
        }

        var resumen = resultado.ElementosEliminados.Count > 0
            ? "Eliminado:\n• " + string.Join("\n• ", resultado.ElementosEliminados)
            : "El perfil no tenía datos locales.";

        if (resultado.Advertencias.Count > 0)
        {
            resumen += "\n\n" + string.Join("\n", resultado.Advertencias);
        }

        var restantes = _servicioConfig.ListarPerfiles()
            .Where(p => !p.Equals(perfil, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        CargarListaPerfiles(excluirPerfil: perfil);
        PerfilSeleccionado = Perfiles.FirstOrDefault()
            ?? restantes.FirstOrDefault()
            ?? "General";
        if (!Perfiles.Contains(PerfilSeleccionado))
        {
            Perfiles.Add(PerfilSeleccionado);
        }

        CargarConfiguracionPerfilActual();
        ReiniciarRegistroPerfil();
        Estadisticas.ReiniciarPresentacion();
        LimpiarVistaTelemetria();
        ActualizarEstadoBotones();
        _temporizador?.Start();

        await MostrarAvisoAsync(resumen, "Perfil eliminado");
    }

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private async Task NuevoPerfilAsync()
    {
        var decision = await PreguntarCambiosSinGuardarAsync("crear otro perfil");
        if (decision == DecisionCambiosPendientes.Cancelar)
        {
            return;
        }

        if (decision == DecisionCambiosPendientes.GuardarYContinuar)
        {
            await GuardarAsync();
        }
        else if (decision == DecisionCambiosPendientes.ContinuarSinGuardar)
        {
            DescartarCambiosPendientes();
        }

        var cuadro = new ContentDialog
        {
            Title = "Nuevo perfil",
            PrimaryButtonText = "Crear",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.Window.Content.XamlRoot
        };

        var caja = new TextBox
        {
            PlaceholderText = "Ej. Trabajo o Fotos",
            Margin = new Thickness(0, 8, 0, 0)
        };
        cuadro.Content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Se creará la configuración por defecto en:\n%LOCALAPPDATA%\\SManager2\\Perfiles configuracion\\<nombre>\\configuracion.json",
                    TextWrapping = TextWrapping.WrapWholeWords
                },
                caja
            }
        };

        var resultado = await cuadro.ShowAsync();
        if (resultado != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            ServicioConfiguracionGui.ValidarNombrePerfil(caja.Text.Trim());
            _servicioConfig.CrearPerfil(caja.Text.Trim());
            PerfilSeleccionado = caja.Text.Trim();
            CargarListaPerfiles();
            CargarConfiguracionPerfilActual();
        }
        catch (Exception ex)
        {
            await MostrarAvisoAsync(ex.Message, "No se pudo crear el perfil");
        }
    }

    /// <summary>Guarda la configuración en disco y, si el demonio está activo, le solicita recargar en caliente.</summary>
    [RelayCommand]
    private async Task GuardarAsync()
    {
        await GuardarYRecargarSiActivoAsync();
        var sufijo = DemonioEnEjecucion
            ? "\n\nEl demonio activo ha recargado los cambios automáticamente."
            : string.Empty;
        await MostrarAvisoAsync($"Configuración guardada en:\n{_rutaConfig}{sufijo}", "Configuración guardada");
    }

    /// <summary>Guarda sin diálogo modal y notifica al demonio si está en ejecución.</summary>
    private async Task GuardarYRecargarSiActivoAsync()
    {
        GuardarSilencioso();
        if (DemonioEnEjecucion)
        {
            try
            {
                await _ipc.EnviarComandoAsync(PerfilActual(), ComandoControl.Recargar);
            }
            catch
            {
                // Si el IPC falla puntualmente, el watcher de tiempo de modificación del motor también detecta el guardado.
            }
        }
    }

    private void GuardarSilencioso()
    {
        LeerConfiguracionDesdeUi();
        _servicioConfig.Guardar(_rutaConfig, _configuracion);
        MarcarComoGuardado();
    }

    [RelayCommand]
    private async Task LimpiarDatosPerfilAsync()
    {
        var perfil = PerfilActual();
        if (_ipc.EstaDemonioEnEjecucion(perfil))
        {
            await MostrarAvisoAsync(
                "Detén el demonio antes de limpiar.\n\nCon el servicio en marcha el log sigue abierto y la telemetría se regenera al instante.",
                "Demonio activo");
            return;
        }

        var cuadro = new ContentDialog
        {
            Title = "Limpiar datos del perfil",
            Content = new TextBlock
            {
                Text =
                    $"Se vaciarán los datos derivados del perfil «{perfil}»:\n\n"
                    + "• Archivo de log en disco\n"
                    + "• Telemetría IPC (Monitor, actividad, estadísticas)\n"
                    + "• Comandos pendientes (control.json)\n"
                    + "• Contadores copiados/errores en la configuración del perfil\n\n"
                    + "No se borran los pares ni los archivos ya copiados en destino.",
                TextWrapping = TextWrapping.WrapWholeWords
            },
            PrimaryButtonText = "Limpiar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.Window.Content.XamlRoot
        };

        if (await cuadro.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var resultado = ServicioLimpiezaDatosPerfil.Limpiar(perfil);

        foreach (var fila in Pares)
        {
            fila.TotalCopiados = 0;
            fila.TotalErrores = 0;
        }

        LeerConfiguracionDesdeUi();
        foreach (var par in _configuracion.Pares)
        {
            par.TotalCopiados = 0;
            par.TotalErrores = 0;
        }

        _servicioConfig.Guardar(_rutaConfig, _configuracion);
        MarcarComoGuardado();

        ReiniciarRegistroPerfil();
        Estadisticas.ReiniciarPresentacion();
        LimpiarVistaTelemetria();

        var resumen = resultado.ElementosLimpiados.Count > 0
            ? "Eliminado o vaciado:\n• " + string.Join("\n• ", resultado.ElementosLimpiados)
            : "No había archivos de telemetría ni log que limpiar.";

        if (resultado.Errores.Count > 0)
        {
            resumen += "\n\nAdvertencias:\n• " + string.Join("\n• ", resultado.Errores);
        }

        resumen += "\n\nContadores del perfil reiniciados a cero.";

        await MostrarAvisoAsync(
            resumen,
            resultado.Exito ? "Limpieza completada" : "Limpieza parcial");
    }

    private string PerfilActual() =>
        string.IsNullOrWhiteSpace(PerfilSeleccionado) ? "General" : PerfilSeleccionado.Trim();

    private void CargarListaPerfiles(string? excluirPerfil = null)
    {
        var actual = PerfilActual();
        Perfiles.Clear();
        var lista = _servicioConfig.ListarPerfiles()
            .Where(p => excluirPerfil is null || !p.Equals(excluirPerfil, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!lista.Contains(actual, StringComparer.OrdinalIgnoreCase)
            && (excluirPerfil is null || !actual.Equals(excluirPerfil, StringComparison.OrdinalIgnoreCase)))
        {
            lista.Add(actual);
        }

        foreach (var p in lista.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            Perfiles.Add(p);
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private async Task ElegirRutaConfigExistenteAsync()
    {
        if (DemonioEnEjecucion)
        {
            await MostrarAvisoAsync("Detén el demonio antes de cambiar la ubicación del JSON.", "Demonio activo");
            return;
        }

        var elegida = await ServicioSelectorArchivoJson.ElegirArchivoExistenteAsync(_rutaConfig);
        if (string.IsNullOrWhiteSpace(elegida))
        {
            return;
        }

        try
        {
            _servicioConfig.EstablecerRutaPersonalizada(PerfilActual(), elegida);
            CargarConfiguracionPerfilActual();
            await MostrarAvisoAsync($"El perfil usará:\n{elegida}", "Configuración personalizada");
        }
        catch (Exception ex)
        {
            await MostrarAvisoAsync(ex.Message, "No se pudo cambiar la ruta");
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private async Task CrearRutaConfigPersonalizadaAsync()
    {
        if (DemonioEnEjecucion)
        {
            await MostrarAvisoAsync("Detén el demonio antes de cambiar la ubicación del JSON.", "Demonio activo");
            return;
        }

        var destino = await ServicioSelectorArchivoJson.ElegirRutaGuardadoAsync(_rutaConfig);
        if (string.IsNullOrWhiteSpace(destino))
        {
            return;
        }

        try
        {
            LeerConfiguracionDesdeUi();
            _servicioConfig.Guardar(destino, _configuracion);
            _servicioConfig.EstablecerRutaPersonalizada(PerfilActual(), destino);
            CargarConfiguracionPerfilActual();
            await MostrarAvisoAsync($"Configuración guardada en:\n{destino}", "Ubicación personalizada");
        }
        catch (Exception ex)
        {
            await MostrarAvisoAsync(ex.Message, "No se pudo crear la configuración");
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeEditarConfig))]
    private async Task RestablecerRutaConfigPorDefectoAsync()
    {
        if (DemonioEnEjecucion)
        {
            await MostrarAvisoAsync("Detén el demonio antes de cambiar la ubicación del JSON.", "Demonio activo");
            return;
        }

        if (!UsaRutaConfigPersonalizada)
        {
            await MostrarAvisoAsync("Este perfil ya usa la ubicación por defecto.", "Ubicación por defecto");
            return;
        }

        var cuadro = new ContentDialog
        {
            Title = "Restaurar ubicación por defecto",
            Content = new TextBlock
            {
                Text =
                    "El perfil volverá a leer y guardar en la carpeta estándar de Perfiles configuracion.\n\n"
                    + "El archivo personalizado no se borra del disco.",
                TextWrapping = TextWrapping.WrapWholeWords
            },
            PrimaryButtonText = "Restaurar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.Window.Content.XamlRoot
        };

        if (await cuadro.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            _servicioConfig.RestablecerRutaPorDefecto(PerfilActual());
            CargarConfiguracionPerfilActual();
            await MostrarAvisoAsync($"Ubicación por defecto:\n{RutaConfiguracionPorDefecto}", "Restaurado");
        }
        catch (Exception ex)
        {
            await MostrarAvisoAsync(ex.Message, "No se pudo restaurar");
        }
    }

    private void CargarConfiguracionPerfilActual()
    {
        _suprimirMarcadoSucio = true;
        try
        {
            CargarConfiguracionPerfilActualInterno();
            MarcarComoGuardado();
        }
        finally
        {
            _suprimirMarcadoSucio = false;
        }
    }

    /// <summary>
    /// Carga en la interfaz todos los datos del perfil desde el JSON, incluyendo el Modo de sincronización
    /// y el umbral del Guardián Antidesastre.
    /// </summary>
    private void CargarConfiguracionPerfilActualInterno()
    {
        var perfil = PerfilActual();
        _rutaConfig = _servicioConfig.AsegurarConfiguracionPerfil(perfil);
        RutaConfiguracion = _rutaConfig;
        RutaConfiguracionPorDefecto = _servicioConfig.ObtenerRutaPorDefecto(perfil);
        UsaRutaConfigPersonalizada = _servicioConfig.UsaRutaPersonalizada(perfil);
        TextoModoRutaConfig = UsaRutaConfigPersonalizada ? "Modo: ubicación personalizada" : "Modo: ubicación por defecto";
        _configuracion = _servicioConfig.Cargar(_rutaConfig);

        Pares.Clear();
        foreach (var par in _configuracion.Pares)
        {
            Pares.Add(new ParFilaViewModel
            {
                IdPar = par.IdPar,
                Habilitado = par.Habilitado,
                Pausado = par.Pausado,
                Nombre = par.Nombre,
                RutaOrigen = par.RutaOrigen,
                RutaDestino = par.RutaDestino,
                FiltroInclusion = par.FiltroInclusion,
                FiltroExclusion = par.FiltroExclusion,
                IntervaloPollingSegundos = par.IntervaloPollingSegundos,
                Modo = par.Modo,
                BorrarEnOrigen = par.Modo == ModoSincronizacion.AcumulativoConBorradoOrigen,
                UmbralPurgaMasivaEspejo = par.UmbralPurgaMasivaEspejo <= 0 ? 50 : par.UmbralPurgaMasivaEspejo,
                TotalCopiados = par.TotalCopiados,
                TotalErrores = par.TotalErrores
            });
        }

        IntervaloPollingSegundos = _configuracion.IntervaloPollingSegundos;
        SegundosEstabilidadArchivo = _configuracion.SegundosEstabilidadArchivo;
        NumCopiadoresParalelos = _configuracion.NumCopiadoresParalelos;
        NumHidratadoresParalelos = _configuracion.NumHidratadoresParalelos;
        TimeoutHidratacionSegundos = _configuracion.TimeoutHidratacionSegundos;
        IntervaloPublicacionEstadoMs = _configuracion.IntervaloPublicacionEstadoMs;
        ActualizarFiltrosParRegistro();
        ActualizarEstadoVacioPares();
        ActualizarPanelInicio(null);
        _ = ActualizarTamanosDestinoInternoAsync(forzar: true);
    }

    /// <summary>
    /// Transfiere los valores actuales de la interfaz hacia el modelo de configuración que se escribe en disco.
    /// Conserva íntegramente Modo, BorrarEnOrigen y UmbralPurgaMasivaEspejo.
    /// </summary>
    private void LeerConfiguracionDesdeUi()
    {
        _configuracion.IntervaloPollingSegundos = IntervaloPollingSegundos;
        _configuracion.SegundosEstabilidadArchivo = SegundosEstabilidadArchivo;
        _configuracion.NumCopiadoresParalelos = NumCopiadoresParalelos;
        _configuracion.NumHidratadoresParalelos = NumHidratadoresParalelos;
        _configuracion.TimeoutHidratacionSegundos = TimeoutHidratacionSegundos;
        _configuracion.IntervaloPublicacionEstadoMs = IntervaloPublicacionEstadoMs;

        _configuracion.Pares = Pares.Select(fila => new ParSincronizacion
        {
            IdPar = fila.IdPar,
            Habilitado = fila.Habilitado,
            Pausado = fila.Pausado,
            Nombre = fila.Nombre,
            RutaOrigen = fila.RutaOrigen,
            RutaDestino = fila.RutaDestino,
            FiltroInclusion = fila.FiltroInclusion,
            FiltroExclusion = fila.FiltroExclusion,
            IntervaloPollingSegundos = fila.IntervaloPollingSegundos,
            Modo = fila.Modo,
            BorrarEnOrigen = fila.Modo == ModoSincronizacion.AcumulativoConBorradoOrigen,
            UmbralPurgaMasivaEspejo = fila.UmbralPurgaMasivaEspejo <= 0 ? 50 : fila.UmbralPurgaMasivaEspejo,
            TotalCopiados = fila.TotalCopiados,
            TotalErrores = fila.TotalErrores
        }).ToList();
    }
}
