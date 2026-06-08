using System.Text;
using SManager.Core.Modelos;
using SManager.Gui.Shared;
using SManager.Ipc;
using SManager.Ipc.Modelos;

namespace SManager.Gui;

/// <summary>Panel de control completo: pares, monitor, config avanzada y logs.</summary>
public sealed class FormularioPrincipal : Form
{
    private readonly ServicioIpc _ipc = new();
    private readonly ServicioConfiguracionGui _servicioConfig = new();
    private readonly ControladorDaemon _daemon = new();
    private readonly System.Windows.Forms.Timer _temporizador = new() { Interval = 500 };

    private ConfiguracionAplicacion _configuracion = ServicioConfiguracionGui.CrearPorDefecto();
    private string _rutaConfig = string.Empty;
    private long _posicionLog;

    private readonly ComboBox _comboPerfiles = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 160 };
    private readonly Button _botonNuevoPerfil = UtilidadesBotones.Crear("Nuevo perfil");
    private readonly TextBox _cajaRutaConfig = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Label _etiquetaEstado = new() { AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
    private readonly Label _etiquetaResumen = new() { AutoSize = true };
    private readonly Label _etiquetaPolling = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly Button _botonIniciar = UtilidadesBotones.Crear("Iniciar");
    private readonly Button _botonDetener = UtilidadesBotones.Crear("Detener");
    private readonly Button _botonRecargar = UtilidadesBotones.Crear("Recargar");
    private readonly Button _botonGuardar = UtilidadesBotones.Crear("Guardar");

    private readonly DataGridView _gridPares = new();

    private readonly NumericUpDown _numPolling = new() { Minimum = 30, Maximum = 3600, Width = 80, Increment = 10 };
    private readonly NumericUpDown _numEstabilidad = new() { Minimum = 1, Maximum = 30, Width = 60 };
    private readonly NumericUpDown _numCopiadores = new() { Minimum = 1, Maximum = 32, Width = 60 };
    private readonly NumericUpDown _numHidratadores = new() { Minimum = 1, Maximum = 16, Width = 60 };
    private readonly NumericUpDown _numTimeoutHidratacion = new() { Minimum = 30, Maximum = 3600, Width = 70 };
    private readonly NumericUpDown _numPublicacionMs = new() { Minimum = 200, Maximum = 5000, Width = 70, Increment = 100 };

    private readonly DataGridView _gridMonitorPares = new();
    private readonly DataGridView _gridCopiando = new();
    private readonly DataGridView _gridActividad = new();
    private readonly TextBox _cajaLog = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font("Consolas", 9F)
    };

    public FormularioPrincipal()
    {
        Text = "SManager 2.0";
        Size = new Size(1150, 750);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 600);
        Font = new Font("Segoe UI", 9F);

        ConstruirInterfaz();
        CargarPerfiles();
        CargarConfiguracionPerfilActual();
        ActualizarBotones();

        _temporizador.Tick += async (_, _) => await ActualizarVistaAsync();
        _temporizador.Start();

        _comboPerfiles.SelectedIndexChanged += (_, _) => CambiarPerfil();
        _comboPerfiles.Leave += (_, _) => CargarPerfiles();
        _botonNuevoPerfil.Click += (_, _) => CrearNuevoPerfil();
        _botonIniciar.Click += async (_, _) => await IniciarAsync();
        _botonDetener.Click += async (_, _) => await DetenerAsync();
        _botonRecargar.Click += async (_, _) => await RecargarAsync();
        _botonGuardar.Click += (_, _) => GuardarConfiguracion(mostrarMensaje: true);
    }

    private void ConstruirInterfaz()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var panelSuperior = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = UtilidadesInterfaz.MargenContenedor,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        panelSuperior.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panelSuperior.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panelSuperior.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panelSuperior.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panelSuperior.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Fila 1: selector de perfil
        var panelPerfil = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
            Margin = new Padding(0, 0, 0, 6)
        };
        _comboPerfiles.Margin = new Padding(0, 4, 8, 4);
        _comboPerfiles.MinimumSize = new Size(140, UtilidadesInterfaz.AltoMinimoBoton);
        panelPerfil.Controls.Add(new Label { Text = "Perfil:", AutoSize = true, Margin = new Padding(0, 8, 6, 4) });
        panelPerfil.Controls.Add(_comboPerfiles);
        panelPerfil.Controls.Add(_botonNuevoPerfil);

        // Fila 1 derecha: botones de control (pueden envolver a segunda línea si falta ancho)
        var panelAcciones = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
            Margin = new Padding(0, 0, 0, 6)
        };
        panelAcciones.Controls.AddRange([_botonIniciar, _botonDetener, _botonRecargar, _botonGuardar]);

        // Fila 2: ruta de configuración
        var panelRuta = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 6)
        };
        panelRuta.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
        panelRuta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panelRuta.Controls.Add(new Label { Text = "Config:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 0, 0) }, 0, 0);
        _cajaRutaConfig.Margin = new Padding(0, 4, 0, 4);
        _cajaRutaConfig.MinimumSize = new Size(0, UtilidadesInterfaz.AltoMinimoBoton);
        panelRuta.Controls.Add(_cajaRutaConfig, 1, 0);

        // Fila 3: telemetría
        var panelTelemetria = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        _etiquetaEstado.Margin = new Padding(0, 0, 0, 2);
        _etiquetaResumen.Margin = new Padding(0, 0, 0, 2);
        _etiquetaPolling.Margin = new Padding(0, 0, 0, 0);
        panelTelemetria.Controls.AddRange([_etiquetaEstado, _etiquetaResumen, _etiquetaPolling]);

        panelSuperior.Controls.Add(panelPerfil, 0, 0);
        panelSuperior.Controls.Add(panelAcciones, 1, 0);
        panelSuperior.SetColumnSpan(panelRuta, 2);
        panelSuperior.Controls.Add(panelRuta, 0, 1);
        panelSuperior.SetColumnSpan(panelTelemetria, 2);
        panelSuperior.Controls.Add(panelTelemetria, 0, 2);

        var pestanas = new TabControl { Dock = DockStyle.Fill, Padding = new Point(4, 4) };

        var tabPares = new TabPage("Pares") { Padding = UtilidadesInterfaz.MargenPestana };
        ConfigurarGridPares();
        var panelParesBotones = CrearPanelBotonesPares();
        var panelPares = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(0) };
        panelPares.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panelPares.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panelPares.Controls.Add(_gridPares, 0, 0);
        panelPares.Controls.Add(panelParesBotones, 0, 1);
        tabPares.Controls.Add(panelPares);

        var tabMonitor = new TabPage("Monitor") { Padding = UtilidadesInterfaz.MargenPestana };
        ConfigurarGridsMonitor();
        var panelMonitor = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(0)
        };
        panelMonitor.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
        panelMonitor.RowStyles.Add(new RowStyle(SizeType.Percent, 22));
        panelMonitor.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        panelMonitor.Controls.Add(_gridMonitorPares, 0, 0);
        panelMonitor.Controls.Add(_gridCopiando, 0, 1);
        panelMonitor.Controls.Add(_gridActividad, 0, 2);
        tabMonitor.Controls.Add(panelMonitor);

        var tabAvanzado = new TabPage("Avanzado") { Padding = UtilidadesInterfaz.MargenPestana };
        tabAvanzado.Controls.Add(CrearPanelAvanzado());

        var tabLog = new TabPage("Registro") { Padding = UtilidadesInterfaz.MargenPestana };
        _cajaLog.Dock = DockStyle.Fill;
        _cajaLog.Margin = new Padding(0);
        tabLog.Controls.Add(_cajaLog);

        pestanas.TabPages.AddRange([tabPares, tabMonitor, tabAvanzado, tabLog]);

        layout.Controls.Add(panelSuperior, 0, 0);
        layout.Controls.Add(pestanas, 0, 1);
        Controls.Add(layout);
    }

    private void ConfigurarGridPares() => UtilidadesInterfaz.ConfigurarGridPares(_gridPares);

    private FlowLayoutPanel CrearPanelBotonesPares()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = UtilidadesInterfaz.MargenBarraBotones
        };

        var btnAnadir = UtilidadesBotones.Crear("Añadir");
        var btnQuitar = UtilidadesBotones.Crear("Quitar");
        var btnOrigen = UtilidadesBotones.Crear("Origen…");
        var btnDestino = UtilidadesBotones.Crear("Destino…");
        var btnValidar = UtilidadesBotones.Crear("Validar rutas");

        btnAnadir.Click += (_, _) => AnadirPar();
        btnQuitar.Click += (_, _) => QuitarPar();
        btnOrigen.Click += (_, _) => ExaminarCarpeta("origen");
        btnDestino.Click += (_, _) => ExaminarCarpeta("destino");
        btnValidar.Click += (_, _) => ValidarRutas();

        panel.Controls.AddRange([btnAnadir, btnQuitar, btnOrigen, btnDestino, btnValidar]);
        return panel;
    }

    private Panel CrearPanelAvanzado()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            Padding = UtilidadesInterfaz.MargenContenedor,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        AgregarFilaAvanzada(panel, "Polling seguridad (s)", _numPolling);
        AgregarFilaAvanzada(panel, "Estabilidad archivo (s)", _numEstabilidad);
        AgregarFilaAvanzada(panel, "Copiadores paralelos", _numCopiadores);
        AgregarFilaAvanzada(panel, "Hidratadores paralelos", _numHidratadores);
        AgregarFilaAvanzada(panel, "Timeout hidratación (s)", _numTimeoutHidratacion);
        AgregarFilaAvanzada(panel, "Publicación estado (ms)", _numPublicacionMs);

        var envoltorio = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        envoltorio.Controls.Add(panel);
        return envoltorio;
    }

    private static void AgregarFilaAvanzada(TableLayoutPanel panel, string etiqueta, Control control)
    {
        var fila = panel.RowCount;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = etiqueta, AutoSize = true, Margin = new Padding(0, 6, 12, 6) }, 0, fila);
        control.Margin = new Padding(0, 4, 0, 4);
        panel.Controls.Add(control, 1, fila);
        panel.RowCount = fila + 1;
    }

    private void ConfigurarGridsMonitor()
    {
        UtilidadesInterfaz.ConfigurarGrid(_gridMonitorPares, soloLectura: true,
            ("nombre", "Par", 90, 80),
            ("estado", "Estado", 60, 70),
            ("copiados", "Copiados", 50, 65),
            ("errores", "Errores", 50, 65));

        UtilidadesInterfaz.ConfigurarGrid(_gridCopiando, soloLectura: true,
            ("copiador", "Copiador", 40, 70),
            ("archivo", "Archivo", 140, 120),
            ("par", "Par", 60, 80));

        UtilidadesInterfaz.ConfigurarGrid(_gridActividad, soloLectura: true,
            ("hora", "Hora", 55, 90),
            ("tipo", "Tipo", 45, 70),
            ("archivo", "Archivo", 130, 120),
            ("par", "Par", 60, 80));
    }

    private string PerfilActual()
    {
        var t = _comboPerfiles.Text.Trim();
        return string.IsNullOrWhiteSpace(t) ? "General" : t;
    }

    private void CargarPerfiles()
    {
        var actual = PerfilActual();
        var lista = _servicioConfig.ListarPerfiles().ToList();
        if (!lista.Contains(actual, StringComparer.OrdinalIgnoreCase))
        {
            lista.Add(actual);
        }

        _comboPerfiles.Items.Clear();
        foreach (var p in lista.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            _comboPerfiles.Items.Add(p);
        }

        _comboPerfiles.Text = actual;
    }

    private void CrearNuevoPerfil()
    {
        var nombre = DialogoNuevoPerfil.Mostrar(this);
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return;
        }

        try
        {
            ServicioConfiguracionGui.ValidarNombrePerfil(nombre);
            _servicioConfig.CrearPerfil(nombre);
            _comboPerfiles.Text = nombre;
            CargarPerfiles();
            CargarConfiguracionPerfilActual();
            MessageBox.Show(
                $"Perfil '{nombre}' creado.\n\nConfiguración:\n{RutasDatos.ObtenerRutaConfiguracionUsuario(nombre)}",
                "SManager 2.0",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "No se pudo crear el perfil", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CambiarPerfil()
    {
        LeerConfiguracionDesdeUi();
        CargarConfiguracionPerfilActual();
        ActualizarBotones();
    }

    private void CargarConfiguracionPerfilActual()
    {
        var perfil = PerfilActual();
        _rutaConfig = _servicioConfig.CrearPerfil(perfil);
        _cajaRutaConfig.Text = _rutaConfig;
        _configuracion = _servicioConfig.Cargar(_rutaConfig);
        PoblarGridPares();
        PoblarAvanzado();
    }

    private void PoblarGridPares()
    {
        _gridPares.Rows.Clear();
        foreach (var par in _configuracion.Pares)
        {
            _gridPares.Rows.Add(
                par.Habilitado,
                par.Pausado,
                par.Nombre,
                par.RutaOrigen,
                par.RutaDestino,
                par.FiltroInclusion,
                par.FiltroExclusion);
            _gridPares.Rows[^1].Tag = par.IdPar;
        }
    }

    private void PoblarAvanzado()
    {
        _numPolling.Value = Math.Clamp(_configuracion.IntervaloPollingSegundos, (int)_numPolling.Minimum, (int)_numPolling.Maximum);
        _numEstabilidad.Value = Math.Clamp(_configuracion.SegundosEstabilidadArchivo, (int)_numEstabilidad.Minimum, (int)_numEstabilidad.Maximum);
        _numCopiadores.Value = Math.Clamp(_configuracion.NumCopiadoresParalelos, (int)_numCopiadores.Minimum, (int)_numCopiadores.Maximum);
        _numHidratadores.Value = Math.Clamp(_configuracion.NumHidratadoresParalelos, (int)_numHidratadores.Minimum, (int)_numHidratadores.Maximum);
        _numTimeoutHidratacion.Value = Math.Clamp(_configuracion.TimeoutHidratacionSegundos, (int)_numTimeoutHidratacion.Minimum, (int)_numTimeoutHidratacion.Maximum);
        _numPublicacionMs.Value = Math.Clamp(_configuracion.IntervaloPublicacionEstadoMs, (int)_numPublicacionMs.Minimum, (int)_numPublicacionMs.Maximum);
    }

    private void LeerConfiguracionDesdeUi()
    {
        _configuracion.IntervaloPollingSegundos = (int)_numPolling.Value;
        _configuracion.SegundosEstabilidadArchivo = (int)_numEstabilidad.Value;
        _configuracion.NumCopiadoresParalelos = (int)_numCopiadores.Value;
        _configuracion.NumHidratadoresParalelos = (int)_numHidratadores.Value;
        _configuracion.TimeoutHidratacionSegundos = (int)_numTimeoutHidratacion.Value;
        _configuracion.IntervaloPublicacionEstadoMs = (int)_numPublicacionMs.Value;

        var pares = new List<ParSincronizacion>();
        foreach (DataGridViewRow fila in _gridPares.Rows)
        {
            if (fila.IsNewRow)
            {
                continue;
            }

            var idPar = fila.Tag as string ?? Guid.NewGuid().ToString();
            var parExistente = _configuracion.Pares.FirstOrDefault(p =>
                string.Equals(p.IdPar, idPar, StringComparison.OrdinalIgnoreCase));

            pares.Add(new ParSincronizacion
            {
                IdPar = idPar,
                Habilitado = fila.Cells["habilitado"].Value is true,
                Pausado = fila.Cells["pausado"].Value is true,
                Nombre = $"{fila.Cells["nombre"].Value}",
                RutaOrigen = $"{fila.Cells["origen"].Value}",
                RutaDestino = $"{fila.Cells["destino"].Value}",
                FiltroInclusion = $"{fila.Cells["inclusion"].Value}",
                FiltroExclusion = $"{fila.Cells["exclusion"].Value}",
                // El grid no muestra contadores; conservar los del JSON al guardar.
                TotalCopiados = parExistente?.TotalCopiados ?? 0,
                TotalErrores = parExistente?.TotalErrores ?? 0
            });
        }

        _configuracion.Pares = pares;
    }

    private void GuardarConfiguracion(bool mostrarMensaje)
    {
        LeerConfiguracionDesdeUi();
        _servicioConfig.Guardar(_rutaConfig, _configuracion);
        if (mostrarMensaje)
        {
            MessageBox.Show($"Configuración guardada en:\n{_rutaConfig}", "SManager 2.0", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void AnadirPar()
    {
        var par = new ParSincronizacion();
        _gridPares.Rows.Add(true, false, par.Nombre, "", "", par.FiltroInclusion, par.FiltroExclusion);
        _gridPares.Rows[^1].Tag = par.IdPar;
    }

    private void QuitarPar()
    {
        if (_gridPares.SelectedRows.Count == 0)
        {
            return;
        }

        _gridPares.Rows.RemoveAt(_gridPares.SelectedRows[0].Index);
    }

    private void ExaminarCarpeta(string columna)
    {
        if (_gridPares.SelectedRows.Count == 0)
        {
            return;
        }

        using var dlg = new FolderBrowserDialog();
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _gridPares.SelectedRows[0].Cells[columna].Value = dlg.SelectedPath;
        }
    }

    private List<string> EvaluarRutas(out bool todasValidas)
    {
        var errores = new List<string>();
        foreach (DataGridViewRow fila in _gridPares.Rows)
        {
            if (fila.IsNewRow || fila.Cells["habilitado"].Value is not true)
            {
                continue;
            }

            var origen = $"{fila.Cells["origen"].Value}".Trim();
            var destino = $"{fila.Cells["destino"].Value}".Trim();
            var nombre = $"{fila.Cells["nombre"].Value}";

            if (string.IsNullOrWhiteSpace(origen) || string.IsNullOrWhiteSpace(destino))
            {
                errores.Add($"- '{nombre}': origen o destino vacíos.");
                fila.DefaultCellStyle.BackColor = Color.MistyRose;
            }
            else if (!Directory.Exists(origen) || !Directory.Exists(destino))
            {
                errores.Add($"- '{nombre}': rutas inaccesibles.");
                fila.DefaultCellStyle.BackColor = Color.MistyRose;
            }
            else
            {
                fila.DefaultCellStyle.BackColor = Color.White;
            }
        }

        todasValidas = errores.Count == 0;
        return errores;
    }

    private void ValidarRutas()
    {
        var errores = EvaluarRutas(out var todasValidas);
        MessageBox.Show(
            todasValidas ? "Todas las rutas existen." : string.Join(Environment.NewLine, errores),
            "Validación",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ActualizarBotones()
    {
        var enEjecucion = _ipc.EstaDemonioEnEjecucion(PerfilActual());
        _botonIniciar.Enabled = !enEjecucion;
        _botonDetener.Enabled = enEjecucion;
        _botonRecargar.Enabled = enEjecucion;
        _botonGuardar.Enabled = !enEjecucion;
        _botonNuevoPerfil.Enabled = !enEjecucion;
        _gridPares.ReadOnly = enEjecucion;
        _etiquetaEstado.Text = enEjecucion ? "[RUN] Ejecutando" : "[STOP] Detenido";
        _etiquetaEstado.ForeColor = enEjecucion ? Color.DarkGreen : Color.DarkRed;
    }

    private async Task ActualizarVistaAsync()
    {
        ActualizarBotones();
        var perfil = PerfilActual();
        var estado = await _ipc.LeerEstadoAsync(perfil).ConfigureAwait(true);
        if (estado is null)
        {
            return;
        }

        _etiquetaResumen.Text =
            $"Cola: {estado.ColaCopiaPendiente}  Únicos: {estado.ArchivosUnicosPendientes}  Dup.ev: {estado.DuplicadosEvitados}  Copiados: {estado.Totales.Copiados}  Errores: {estado.Totales.Errores}";
        _etiquetaPolling.Text = estado.ProximoPollingEnSegundos.HasValue
            ? $"Próximo polling: en {estado.ProximoPollingEnSegundos}s"
            : "Próximo polling: —";

        ActualizarGrid(_gridMonitorPares, estado.Pares.Select(p => new object[] { p.Nombre, p.Estado, p.Copiados, p.Errores }));
        ActualizarGrid(_gridCopiando, estado.CopiasEnCurso.Select(c => new object[] { c.Copiador, c.Archivo, c.IdPar }));
        ActualizarGrid(_gridActividad, estado.ActividadReciente.Select(a => new object[] { a.Hora, a.Tipo, a.Archivo, a.IdPar }));

        ActualizarLog(perfil);
    }

    private static void ActualizarGrid(DataGridView grid, IEnumerable<object[]> filas)
    {
        grid.Rows.Clear();
        foreach (var f in filas)
        {
            grid.Rows.Add(f);
        }
    }

    private void ActualizarLog(string perfil)
    {
        var rutaLog = RutasDatos.ObtenerRutaLog(perfil);
        if (!File.Exists(rutaLog))
        {
            return;
        }

        try
        {
            using var flujo = new FileStream(rutaLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (flujo.Length < _posicionLog)
            {
                _posicionLog = 0;
                _cajaLog.Clear();
            }

            flujo.Seek(_posicionLog, SeekOrigin.Begin);
            using var lector = new StreamReader(flujo, Encoding.UTF8);
            var nuevo = lector.ReadToEnd();
            _posicionLog = flujo.Length;
            if (!string.IsNullOrEmpty(nuevo))
            {
                _cajaLog.AppendText(nuevo);
                if (_cajaLog.TextLength > 200_000)
                {
                    _cajaLog.Text = _cajaLog.Text[^100_000..];
                }
            }
        }
        catch
        {
            // No bloquear la UI por el log.
        }
    }

    private async Task IniciarAsync()
    {
        GuardarConfiguracion(mostrarMensaje: false);

        var erroresRutas = EvaluarRutas(out var rutasOk);
        if (!rutasOk)
        {
            MessageBox.Show(
                "Corrige las rutas antes de iniciar:" + Environment.NewLine + Environment.NewLine
                + string.Join(Environment.NewLine, erroresRutas),
                "Rutas no válidas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var perfil = PerfilActual();
        var (codigo, salida, error) = await _daemon.EjecutarAsync(
            $"start -perfil \"{perfil}\" -configpath \"{_rutaConfig}\"").ConfigureAwait(true);

        if (codigo != 0)
        {
            MessageBox.Show(string.IsNullOrWhiteSpace(error) ? salida : error, "Error al iniciar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        _posicionLog = 0;
        CargarPerfiles();
        ActualizarBotones();
    }

    private async Task DetenerAsync()
    {
        var (codigo, salida, error) = await _daemon.EjecutarAsync($"stop -perfil \"{PerfilActual()}\"").ConfigureAwait(true);
        if (codigo != 0)
        {
            MessageBox.Show(string.IsNullOrWhiteSpace(error) ? salida : error, "Error al detener", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        ActualizarBotones();
    }

    private async Task RecargarAsync()
    {
        // Solo pide al demonio releer el JSON del disco. No guarda la GUI aquí:
        // Guardar antes sobrescribía el fichero con el estado congelado del grid (readonly).
        var (codigo, salida, error) = await _daemon.EjecutarAsync($"reload -perfil \"{PerfilActual()}\"").ConfigureAwait(true);
        if (codigo != 0)
        {
            MessageBox.Show(string.IsNullOrWhiteSpace(error) ? salida : error, "Error al recargar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Sincronizar la GUI con lo que hay en disco tras la recarga del demonio.
        CargarConfiguracionPerfilActual();
    }
}
