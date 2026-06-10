using System.Drawing;
using System.Windows.Forms;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Icono y menú contextual en la bandeja de Windows (NotifyIcon de WinForms).</summary>
public sealed class ServicioBandejaSistema : IDisposable
{
    private NotifyIcon? _iconoBandeja;
    private ContextMenuStrip? _menuContextual;
    private bool _puedeIniciar = true;
    private bool _puedeDetener;
    private string _textoTooltip = "SManager 2.0";

    /// <summary>Carga el icono y lo muestra en la bandeja del sistema.</summary>
    public void Inicializar(string rutaIcono)
    {
        if (_iconoBandeja is not null)
        {
            return;
        }

        _menuContextual = CrearMenuContextual();

        _iconoBandeja = new NotifyIcon
        {
            Icon = CargarIcono(rutaIcono),
            Text = RecortarTooltip(_textoTooltip),
            Visible = true,
            ContextMenuStrip = _menuContextual
        };

        _iconoBandeja.DoubleClick += (_, _) => ServicioAccionesBandeja.SolicitarAbrirVentana();
    }

    public void ActualizarEstado(string textoTooltip, bool puedeIniciar, bool puedeDetener)
    {
        _textoTooltip = string.IsNullOrWhiteSpace(textoTooltip) ? "SManager 2.0" : textoTooltip;
        _puedeIniciar = puedeIniciar;
        _puedeDetener = puedeDetener;

        if (_iconoBandeja is null)
        {
            return;
        }

        _iconoBandeja.Text = RecortarTooltip(_textoTooltip);
        ReconstruirMenuContextual();
    }

    public void Dispose()
    {
        if (_iconoBandeja is not null)
        {
            _iconoBandeja.Visible = false;
            _iconoBandeja.Dispose();
            _iconoBandeja = null;
        }

        _menuContextual?.Dispose();
        _menuContextual = null;
    }

    private ContextMenuStrip CrearMenuContextual()
    {
        var menu = new ContextMenuStrip();
        AgregarEntradasMenu(menu);
        return menu;
    }

    private void ReconstruirMenuContextual()
    {
        if (_iconoBandeja is null)
        {
            return;
        }

        _menuContextual?.Dispose();
        _menuContextual = CrearMenuContextual();
        _iconoBandeja.ContextMenuStrip = _menuContextual;
    }

    private void AgregarEntradasMenu(ContextMenuStrip menu)
    {
        menu.Items.Add(new ToolStripLabel("SManager 2.0") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("Abrir SManager", null, (_, _) => ServicioAccionesBandeja.SolicitarAbrirVentana());

        var itemIniciar = new ToolStripMenuItem("Sincronizar ahora", null, (_, _) =>
        {
            if (_puedeIniciar)
            {
                ServicioAccionesBandeja.SolicitarIniciar();
            }
        })
        {
            Enabled = _puedeIniciar
        };
        menu.Items.Add(itemIniciar);

        var itemDetener = new ToolStripMenuItem("Detener sincronización", null, (_, _) =>
        {
            if (_puedeDetener)
            {
                ServicioAccionesBandeja.SolicitarDetener();
            }
        })
        {
            Enabled = _puedeDetener
        };
        menu.Items.Add(itemDetener);

        menu.Items.Add("Ver actividad (Monitor)", null, (_, _) => ServicioAccionesBandeja.SolicitarVerMonitor());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => ServicioAccionesBandeja.SolicitarSalir());
    }

    private static string RecortarTooltip(string texto) =>
        texto.Length <= 63 ? texto : texto[..60] + "…";

    private static Icon CargarIcono(string rutaIcono)
    {
        if (File.Exists(rutaIcono))
        {
            try
            {
                return new Icon(rutaIcono);
            }
            catch
            {
                // Continuar con icono del sistema.
            }
        }

        return SystemIcons.Application;
    }
}
