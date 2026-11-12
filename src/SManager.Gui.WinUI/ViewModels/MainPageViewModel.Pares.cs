using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SManager.Core.Utilidades;
using SManager.Gui.Shared.Modelos;
using SManager.Gui.Shared.Servicios;
using SManager.Gui.WinUI.Models;
using SManager.Gui.WinUI.Servicios;
using SManager.Ipc;

namespace SManager.Gui.WinUI.ViewModels;

/// <summary>
/// Parte del ViewModel encargada de los pares de sincronización, el control del demonio (iniciar, detener, recargar)
/// y el simulador de «Analizar cambios» con vista previa detallada de archivos.
/// </summary>
public partial class MainPageViewModel
{
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task IniciarAsync()
    {
        if (!PuedeIniciar)
        {
            return;
        }

        try
        {
            LeerConfiguracionDesdeUi();
            _servicioConfig.Guardar(_rutaConfig, _configuracion);
            MarcarComoGuardado();

            if (!Pares.Any(p => p.Habilitado))
            {
                await MostrarAvisoAsync(
                    "Añade al menos un par activo antes de iniciar el demonio.",
                    "Sin pares activos");
                return;
            }

            var errores = EvaluarRutas(out var ok);
            if (!ok)
            {
                await MostrarAvisoAsync(string.Join('\n', errores), "Rutas no válidas");
                return;
            }

            var (codigo, salida, error) = await _daemon.EjecutarAsync(
                $"start -perfil \"{PerfilActual()}\"");

            if (codigo != 0)
            {
                await MostrarAvisoAsync(string.IsNullOrWhiteSpace(error) ? salida : error, "Error al iniciar");
            }

            _posicionLog = 0;
            _textoRegistroCrudo = string.Empty;
            LineasRegistro.Clear();
            CargarListaPerfiles();
        }
        finally
        {
            await RefrescarEstadoDemonioEnUiAsync();
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task DetenerAsync()
    {
        if (!PuedeDetener)
        {
            return;
        }

        PuedeDetener = false;
        TextoEstado = "Deteniendo…";
        _monitorNotificaciones.MarcarDetencionIntencional();

        try
        {
            var (codigo, salida, error) = await _daemon.EjecutarAsync($"stop -perfil \"{PerfilActual()}\"");
            if (codigo != 0)
            {
                await MostrarAvisoAsync(string.IsNullOrWhiteSpace(error) ? salida : error, "Error al detener");
            }
        }
        finally
        {
            await RefrescarEstadoDemonioEnUiAsync();
            await EsperarLiberacionPidAsync();
        }
    }

    private async Task EsperarLiberacionPidAsync()
    {
        for (var i = 0; i < 200; i++)
        {
            if (!_ipc.EstaDemonioEnEjecucion(PerfilActual()))
            {
                await RefrescarEstadoDemonioEnUiAsync();
                return;
            }

            await Task.Delay(250);
        }

        await RefrescarEstadoDemonioEnUiAsync();
    }

    [RelayCommand]
    private async Task DesbloquearBorradoAdminAsync()
    {
        var exito = ServicioAutenticacionAdmin.SolicitarYValidarCredencialesAdminNativas(
            App.WindowHandle,
            out var mensaje);

        if (exito)
        {
            SesionBorradoDesbloqueada = true;
            if (DemonioEnEjecucion)
            {
                await _ipc.EnviarComandoAsync(
                    PerfilActual(),
                    ComandoControl.DesbloquearBorrado,
                    desbloquearBorrado: true);
            }

            await MostrarAvisoAsync(
                mensaje ?? "Autenticación correcta como administrador local. Borrado en origen activado para esta sesión.",
                "Autenticación confirmada");
        }
        else
        {
            SesionBorradoDesbloqueada = false;
            await MostrarAvisoAsync(
                mensaje ?? "Se requieren credenciales de administrador local válidas para desbloquear el borrado en origen.",
                "Acceso denegado");
        }
    }

    [RelayCommand]
    private async Task AutorizarPurgaMasivaAsync(ParFilaViewModel? par)
    {
        if (par is null)
        {
            return;
        }

        var perfil = PerfilActual();
        await _ipc.EnviarComandoAsync(
            perfil,
            ComandoControl.AutorizarPurgaEspejo,
            idsPares: [par.IdPar]);

        par.TienePurgaMasivaBloqueada = false;
        par.CantidadArchivosPurgaBloqueada = 0;

        await MostrarAvisoAsync(
            $"Purga masiva autorizada para el par «{par.Nombre}». El demonio ejecutará la eliminación de los archivos huérfanos en destino.",
            "Purga autorizada");
    }

    [RelayCommand]
    private async Task ArrancarSeleccionadosAsync()
    {
        var seleccionados = Pares.Where(p => p.Seleccionado).ToList();
        if (seleccionados.Count == 0)
        {
            await MostrarAvisoAsync("Selecciona al menos un par utilizando las casillas de verificación.", "Ningún par seleccionado");
            return;
        }

        foreach (var par in seleccionados)
        {
            par.Habilitado = true;
            par.Pausado = false;
        }

        GuardarSilencioso();

        if (DemonioEnEjecucion)
        {
            var ids = seleccionados.Select(p => p.IdPar).ToList();
            await _ipc.EnviarComandoAsync(PerfilActual(), ComandoControl.IniciarPares, idsPares: ids);
            await MostrarAvisoAsync($"Se han iniciado {ids.Count} pares seleccionados en el demonio activo.", "Pares iniciados");
        }
        else
        {
            await IniciarAsync();
        }
    }

    [RelayCommand]
    private async Task PausarSeleccionadosAsync()
    {
        var seleccionados = Pares.Where(p => p.Seleccionado).ToList();
        if (seleccionados.Count == 0)
        {
            await MostrarAvisoAsync("Selecciona al menos un par utilizando las casillas de verificación.", "Ningún par seleccionado");
            return;
        }

        foreach (var par in seleccionados)
        {
            par.Pausado = true;
        }

        GuardarSilencioso();

        if (DemonioEnEjecucion)
        {
            var ids = seleccionados.Select(p => p.IdPar).ToList();
            await _ipc.EnviarComandoAsync(PerfilActual(), ComandoControl.PausarPares, idsPares: ids);
        }
    }

    [RelayCommand]
    private void SeleccionarTodosPares()
    {
        var todosSeleccionados = Pares.Count > 0 && Pares.All(p => p.Seleccionado);
        foreach (var p in Pares)
        {
            p.Seleccionado = !todosSeleccionados;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RecargarAsync()
    {
        if (!PuedeDetener)
        {
            return;
        }

        if (HayCambiosSinGuardar)
        {
            GuardarSilencioso();
        }

        var (codigo, salida, error) = await _daemon.EjecutarAsync($"reload -perfil \"{PerfilActual()}\"");
        if (codigo != 0)
        {
            await MostrarAvisoAsync(string.IsNullOrWhiteSpace(error) ? salida : error, "Error al recargar");
            return;
        }

        CargarConfiguracionPerfilActual();
    }

    [RelayCommand]
    private async Task AnadirParAsync()
    {
        var nuevo = await ServicioDialogoPar.MostrarAsync(null, esEdicion: false);
        if (nuevo is null)
        {
            return;
        }

        Pares.Add(nuevo);
        ParSeleccionado = nuevo;
        ActualizarFiltrosParRegistro();

        if (DemonioEnEjecucion)
        {
            await GuardarYRecargarSiActivoAsync();
        }
    }

    [RelayCommand]
    private async Task EditarParAsync(ParFilaViewModel? par)
    {
        if (par is null)
        {
            return;
        }

        var editado = await ServicioDialogoPar.MostrarAsync(par, esEdicion: true);
        if (editado is null)
        {
            return;
        }

        CopiarDatosPar(editado, par);
        ActualizarFiltrosParRegistro();

        if (DemonioEnEjecucion)
        {
            await GuardarYRecargarSiActivoAsync();
        }
    }

    [RelayCommand]
    private async Task QuitarParAsync(ParFilaViewModel? par)
    {
        var objetivo = par ?? ParSeleccionado;
        if (objetivo is null)
        {
            return;
        }

        var etiquetaPar = string.IsNullOrWhiteSpace(objetivo.Nombre)
            ? "el par seleccionado"
            : $"«{objetivo.Nombre.Trim()}»";

        var cuadro = new ContentDialog
        {
            Title = "Quitar par",
            Content = new TextBlock
            {
                Text = $"¿Eliminar {etiquetaPar} de la configuración?",
                TextWrapping = TextWrapping.WrapWholeWords
            },
            PrimaryButtonText = "Eliminar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.Window.Content.XamlRoot
        };

        if (await cuadro.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        Pares.Remove(objetivo);
        if (ParSeleccionado == objetivo)
        {
            ParSeleccionado = null;
        }

        ActualizarFiltrosParRegistro();

        if (DemonioEnEjecucion)
        {
            await GuardarYRecargarSiActivoAsync();
        }
    }

    /// <summary>Copia todos los campos editables de un par (incluido Modo y UmbralPurgaMasivaEspejo).</summary>
    private static void CopiarDatosPar(ParFilaViewModel origen, ParFilaViewModel destino)
    {
        destino.Nombre = origen.Nombre;
        destino.RutaOrigen = origen.RutaOrigen;
        destino.RutaDestino = origen.RutaDestino;
        destino.FiltroInclusion = origen.FiltroInclusion;
        destino.FiltroExclusion = origen.FiltroExclusion;
        destino.Habilitado = origen.Habilitado;
        destino.Pausado = origen.Pausado;
        destino.Modo = origen.Modo;
        destino.BorrarEnOrigen = origen.BorrarEnOrigen;
        destino.UmbralPurgaMasivaEspejo = origen.UmbralPurgaMasivaEspejo;
        destino.IntervaloPollingSegundos = origen.IntervaloPollingSegundos;
    }

    [RelayCommand]
    private async Task ValidarRutasAsync()
    {
        var errores = EvaluarRutas(out var ok);
        await MostrarAvisoAsync(
            ok ? "Todas las rutas de los pares activos existen y están disponibles." : string.Join('\n', errores),
            "Validación de rutas");
    }

    /// <summary>Simula la sincronización: cuenta y lista archivos nuevos, modificados y purgas espejo sin tocar el disco.</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task AnalizarCambiosAsync()
    {
        if (AnalizandoCambios)
        {
            return;
        }

        LeerConfiguracionDesdeUi();
        var paresActivos = _configuracion.Pares
            .Where(p => p.Habilitado && !p.Pausado)
            .ToList();

        if (paresActivos.Count == 0)
        {
            await MostrarAvisoAsync(
                "Añade al menos un par activo y no pausado para analizar cambios.",
                "Sin pares activos");
            return;
        }

        AnalizandoCambios = true;
        try
        {
            var resultado = await Task.Run(() =>
                ServicioAnalisisCambios.AnalizarPares(paresActivos, CancellationToken.None))
                .ConfigureAwait(true);

            await MostrarDialogoAnalisisAsync(resultado).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await MostrarAvisoAsync(ex.Message, "No se pudo analizar").ConfigureAwait(true);
        }
        finally
        {
            AnalizandoCambios = false;
        }
    }

    /// <summary>
    /// Construye y muestra el diálogo de vista previa con el resumen numérico y el listado desplegable
    /// de todos los archivos individuales previstos (nuevos, modificados y purgas en modo espejo).
    /// </summary>
    private async Task MostrarDialogoAnalisisAsync(ResultadoAnalisisGlobal resultado)
    {
        var contenido = new StackPanel { Spacing = 12, MinWidth = 540, MaxWidth = 680 };

        contenido.Children.Add(new TextBlock
        {
            Text = "Vista previa simulada: no se ha copiado ni eliminado ningún archivo.",
            TextWrapping = TextWrapping.WrapWholeWords
        });

        var tablaResumen = new Grid { ColumnSpacing = 16, RowSpacing = 6 };
        tablaResumen.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        tablaResumen.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        tablaResumen.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        AgregarFilaAnalisis(tablaResumen, 0, "Acción prevista", "Cantidad", "Tamaño", negrita: true);
        AgregarFilaAnalisis(
            tablaResumen,
            1,
            "Archivos nuevos a copiar",
            resultado.TotalNuevos.ToString("N0"),
            ServicioFormateoEstadisticas.FormatearBytes(resultado.TotalBytesNuevos));
        AgregarFilaAnalisis(
            tablaResumen,
            2,
            "Archivos modificados a actualizar",
            resultado.TotalModificados.ToString("N0"),
            ServicioFormateoEstadisticas.FormatearBytes(resultado.TotalBytesModificados));

        var filaActual = 3;
        if (resultado.TotalHuerfanosEspejo > 0)
        {
            AgregarFilaAnalisis(
                tablaResumen,
                filaActual++,
                "Archivos a eliminar en destino (espejo)",
                resultado.TotalHuerfanosEspejo.ToString("N0"),
                ServicioFormateoEstadisticas.FormatearBytes(resultado.TotalBytesHuerfanosEspejo));
        }

        AgregarFilaAnalisis(
            tablaResumen,
            filaActual++,
            "Omitidos por filtro",
            resultado.TotalOmitidos.ToString("N0"),
            ServicioFormateoEstadisticas.FormatearBytes(resultado.PorPar.Sum(p => p.BytesOmitidosFiltro)));
        AgregarFilaAnalisis(
            tablaResumen,
            filaActual++,
            "Errores de acceso",
            resultado.TotalErrores.ToString("N0"),
            "—");
        AgregarFilaAnalisis(
            tablaResumen,
            filaActual,
            "Total pendiente de copia",
            (resultado.TotalNuevos + resultado.TotalModificados).ToString("N0"),
            ServicioFormateoEstadisticas.FormatearBytes(resultado.TotalBytesPendientes),
            negrita: true);

        contenido.Children.Add(tablaResumen);

        // Estado vacío o listado detallado de archivos concretos
        var archivosPrevistos = resultado.TodosLosArchivosPrevistos;
        if (archivosPrevistos.Count == 0 && resultado.TotalErrores == 0)
        {
            contenido.Children.Add(new InfoBar
            {
                Title = "Todo está al día",
                Message = "Las carpetas de destino ya están sincronizadas con el origen.",
                Severity = InfoBarSeverity.Success,
                IsOpen = true,
                IsClosable = false
            });
        }
        else if (archivosPrevistos.Count > 0)
        {
            var panelArchivos = new StackPanel { Spacing = 4 };
            foreach (var item in archivosPrevistos)
            {
                var filaArchivo = new Grid { ColumnSpacing = 10, Padding = new Thickness(4, 3, 4, 3) };
                filaArchivo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                filaArchivo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                filaArchivo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });

                var txtAccion = new TextBlock
                {
                    Text = item.TipoAccion,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var txtRuta = new TextBlock
                {
                    Text = $"[{item.NombrePar}] {item.RutaRelativa}",
                    FontSize = 12,
                    FontFamily = new FontFamily("Consolas"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    IsTextSelectionEnabled = true,
                    VerticalAlignment = VerticalAlignment.Center
                };
                ToolTipService.SetToolTip(txtRuta, $"{item.NombrePar}: {item.RutaRelativa}");

                var txtTamano = new TextBlock
                {
                    Text = ServicioFormateoEstadisticas.FormatearBytes(item.TamanoBytes),
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };

                Grid.SetColumn(txtAccion, 0);
                Grid.SetColumn(txtRuta, 1);
                Grid.SetColumn(txtTamano, 2);
                filaArchivo.Children.Add(txtAccion);
                filaArchivo.Children.Add(txtRuta);
                filaArchivo.Children.Add(txtTamano);
                panelArchivos.Children.Add(filaArchivo);
            }

            if (resultado.AlgunDetalleTruncado)
            {
                panelArchivos.Children.Add(new TextBlock
                {
                    Text = $"Mostrando los primeros {archivosPrevistos.Count:N0} archivos para mantener la fluidez.",
                    FontSize = 12,
                    Margin = new Thickness(4, 6, 4, 0)
                });
            }

            var scrollDetalle = new ScrollViewer
            {
                MaxHeight = 220,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = panelArchivos
            };

            contenido.Children.Add(new Expander
            {
                Header = $"Ver detalle de archivos ({archivosPrevistos.Count:N0})",
                IsExpanded = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = scrollDetalle
            });
        }

        foreach (var par in resultado.PorPar.Where(p => p.AvisosRiesgo.Count > 0))
        {
            foreach (var aviso in par.AvisosRiesgo)
            {
                contenido.Children.Add(new InfoBar
                {
                    Title = par.NombrePar,
                    Message = aviso,
                    Severity = InfoBarSeverity.Warning,
                    IsOpen = true,
                    IsClosable = false
                });
            }
        }

        var hayCambiosPorAplicar = resultado.TotalNuevos + resultado.TotalModificados + resultado.TotalHuerfanosEspejo > 0;
        var cuadro = new ContentDialog
        {
            Title = "Análisis de cambios",
            Content = contenido,
            CloseButtonText = "Cerrar",
            PrimaryButtonText = PuedeIniciar && hayCambiosPorAplicar ? "Sincronizar ahora" : "Ir a sincronización",
            SecondaryButtonText = PuedeIniciar && hayCambiosPorAplicar ? "Ir a sincronización" : string.Empty,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.Window.Content.XamlRoot
        };

        var decision = await cuadro.ShowAsync();
        if (decision == ContentDialogResult.Primary)
        {
            if (PuedeIniciar && hayCambiosPorAplicar)
            {
                await IniciarAsync();
            }
            else
            {
                NavegarSeccionSolicitado?.Invoke(this, "pares");
            }
        }
        else if (decision == ContentDialogResult.Secondary)
        {
            NavegarSeccionSolicitado?.Invoke(this, "pares");
        }
    }

    private static void AgregarFilaAnalisis(
        Grid tabla,
        int fila,
        string accion,
        string cantidad,
        string tamano,
        bool negrita = false)
    {
        while (tabla.RowDefinitions.Count <= fila)
        {
            tabla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        tabla.Children.Add(CrearCeldaAnalisis(accion, fila, 0, negrita));
        tabla.Children.Add(CrearCeldaAnalisis(cantidad, fila, 1, negrita));
        tabla.Children.Add(CrearCeldaAnalisis(tamano, fila, 2, negrita));
    }

    private static TextBlock CrearCeldaAnalisis(string texto, int fila, int columna, bool negrita)
    {
        var bloque = new TextBlock
        {
            Text = texto,
            FontWeight = negrita ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(bloque, fila);
        Grid.SetColumn(bloque, columna);
        return bloque;
    }

    private List<string> EvaluarRutas(out bool todasValidas)
    {
        var errores = new List<string>();
        foreach (var fila in Pares.Where(p => p.Habilitado))
        {
            if (string.IsNullOrWhiteSpace(fila.RutaOrigen) || string.IsNullOrWhiteSpace(fila.RutaDestino))
            {
                errores.Add($"• «{fila.Nombre}»: la carpeta de origen o destino está vacía.");
            }
            else if (!Directory.Exists(fila.RutaOrigen) || !Directory.Exists(fila.RutaDestino))
            {
                errores.Add($"• «{fila.Nombre}»: la carpeta de origen o destino no existe o no está accesible.");
            }
        }

        todasValidas = errores.Count == 0;
        return errores;
    }
}
