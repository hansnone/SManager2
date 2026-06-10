using System.Runtime.InteropServices;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Icono y menú contextual en la bandeja de Windows (Win32 Shell_NotifyIcon).</summary>
public sealed class ServicioBandejaSistema : IDisposable
{
    private const int IdIconoBandeja = 9001;
    private const int MensajeCallbackBandeja = 0x8000 + 64;
    private const int ComandoAbrir = 2001;
    private const int ComandoIniciar = 2002;
    private const int ComandoDetener = 2003;
    private const int ComandoMonitor = 2004;
    private const int ComandoSalir = 2005;

    private const int NimAdd = 0x00000000;
    private const int NimModify = 0x00000001;
    private const int NimDelete = 0x00000002;
    private const int NifMessage = 0x00000001;
    private const int NifIcon = 0x00000002;
    private const int NifTip = 0x00000004;

    private const uint WmCommand = 0x0111;
    private const uint WmDestroy = 0x0002;
    private const uint WmLButtonDblClk = 0x0203;
    private const uint WmRButtonUp = 0x0205;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;

    private static readonly IntPtr VentanaMensajeEstatica = new(-3);

    private readonly WndProcDelegado _delegadoWndProc;
    private IntPtr _handleVentanaMensaje;
    private IntPtr _handleIcono;
    private bool _iconoVisible;
    private bool _puedeIniciar = true;
    private bool _puedeDetener;
    private string _textoTooltip = "SManager 2.0";

    public ServicioBandejaSistema()
    {
        _delegadoWndProc = ProcesarMensajeVentana;
    }

    /// <summary>Crea la ventana oculta, carga el icono y registra el notify icon.</summary>
    public void Inicializar(string rutaIcono)
    {
        if (_handleVentanaMensaje != IntPtr.Zero)
        {
            return;
        }

        _handleVentanaMensaje = CrearVentanaMensaje();
        _handleIcono = CargarIcono(rutaIcono);
        ActualizarDatosIcono(NimAdd);
        _iconoVisible = true;
    }

    public void ActualizarEstado(string textoTooltip, bool puedeIniciar, bool puedeDetener)
    {
        _textoTooltip = string.IsNullOrWhiteSpace(textoTooltip) ? "SManager 2.0" : textoTooltip;
        _puedeIniciar = puedeIniciar;
        _puedeDetener = puedeDetener;

        if (_iconoVisible)
        {
            ActualizarDatosIcono(NimModify);
        }
    }

    public void Dispose()
    {
        if (_iconoVisible)
        {
            ActualizarDatosIcono(NimDelete);
            _iconoVisible = false;
        }

        if (_handleIcono != IntPtr.Zero)
        {
            DestruirIcono(_handleIcono);
            _handleIcono = IntPtr.Zero;
        }

        if (_handleVentanaMensaje != IntPtr.Zero)
        {
            DestruirVentana(_handleVentanaMensaje);
            _handleVentanaMensaje = IntPtr.Zero;
        }
    }

    private IntPtr CrearVentanaMensaje()
    {
        var nombreClase = "SManager2BandejaMsg_" + Environment.ProcessId;
        var clase = new ClaseVentana
        {
            cbSize = Marshal.SizeOf<ClaseVentana>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_delegadoWndProc),
            hInstance = ObtenerModuloActual(),
            lpszClassName = nombreClase
        };

        RegistrarClaseVentana(ref clase);

        return CrearVentanaEx(
            0,
            nombreClase,
            "SManagerBandeja",
            0,
            0,
            0,
            0,
            0,
            VentanaMensajeEstatica,
            IntPtr.Zero,
            clase.hInstance,
            IntPtr.Zero);
    }

    private IntPtr ProcesarMensajeVentana(IntPtr hWnd, uint mensaje, IntPtr wParam, IntPtr lParam)
    {
        if (mensaje == MensajeCallbackBandeja)
        {
            var eventoRaton = (uint)lParam & 0xFFFF;
            if (eventoRaton == WmLButtonDblClk)
            {
                ServicioAccionesBandeja.SolicitarAbrirVentana();
            }
            else if (eventoRaton == WmRButtonUp)
            {
                MostrarMenuContextual();
            }

            return IntPtr.Zero;
        }

        if (mensaje == WmCommand)
        {
            EjecutarComandoMenu((int)(wParam.ToInt64() & 0xFFFF));
            return IntPtr.Zero;
        }

        if (mensaje == WmDestroy)
        {
            return IntPtr.Zero;
        }

        return DefWindowProc(hWnd, mensaje, wParam, lParam);
    }

    private void MostrarMenuContextual()
    {
        var menu = CrearMenuPopup();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        AgregarElementoMenu(menu, 0, MF_STRING | MF_GRAYED, 0, "SManager 2.0");
        AgregarElementoMenu(menu, 0, MF_SEPARATOR, 0, null);
        AgregarElementoMenu(menu, ComandoAbrir, MF_STRING, ComandoAbrir, "Abrir SManager");
        AgregarElementoMenu(
            menu,
            ComandoIniciar,
            MF_STRING | (_puedeIniciar ? 0 : MF_GRAYED),
            ComandoIniciar,
            "Sincronizar ahora");
        AgregarElementoMenu(
            menu,
            ComandoDetener,
            MF_STRING | (_puedeDetener ? 0 : MF_GRAYED),
            ComandoDetener,
            "Detener sincronización");
        AgregarElementoMenu(menu, ComandoMonitor, MF_STRING, ComandoMonitor, "Ver actividad (Monitor)");
        AgregarElementoMenu(menu, 0, MF_SEPARATOR, 0, null);
        AgregarElementoMenu(menu, ComandoSalir, MF_STRING, ComandoSalir, "Salir");

        ObtenerPosicionCursor(out var punto);
        EstablecerVentanaEnPrimerPlano(_handleVentanaMensaje);

        var comando = TrackPopupMenuEx(
            menu,
            TpmRightButton | TpmReturnCmd,
            punto.X,
            punto.Y,
            _handleVentanaMensaje,
            IntPtr.Zero);

        DestruirMenu(menu);

        if (comando != 0)
        {
            EjecutarComandoMenu(comando);
        }
    }

    private void EjecutarComandoMenu(int comando)
    {
        switch (comando)
        {
            case ComandoAbrir:
                ServicioAccionesBandeja.SolicitarAbrirVentana();
                break;
            case ComandoIniciar when _puedeIniciar:
                ServicioAccionesBandeja.SolicitarIniciar();
                break;
            case ComandoDetener when _puedeDetener:
                ServicioAccionesBandeja.SolicitarDetener();
                break;
            case ComandoMonitor:
                ServicioAccionesBandeja.SolicitarVerMonitor();
                break;
            case ComandoSalir:
                ServicioAccionesBandeja.SolicitarSalir();
                break;
        }
    }

    private void ActualizarDatosIcono(int operacion)
    {
        var datos = new DatosIconoBandeja
        {
            cbSize = Marshal.SizeOf<DatosIconoBandeja>(),
            hWnd = _handleVentanaMensaje,
            uID = IdIconoBandeja,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = MensajeCallbackBandeja,
            hIcon = _handleIcono,
            szTip = RecortarTooltip(_textoTooltip)
        };

        if (!NotificarIconoBandeja(operacion, ref datos))
        {
            // No bloquear la app si la bandeja falla (sesiones remotas, políticas, etc.).
        }
    }

    private static string RecortarTooltip(string texto) =>
        texto.Length <= 127 ? texto : texto[..124] + "…";

    private static IntPtr CargarIcono(string rutaIcono)
    {
        if (File.Exists(rutaIcono))
        {
            var icono = CargarIconoDesdeArchivo(rutaIcono, 0, 16, 16, LrLoadFromFile | LrDefaultSize);
            if (icono != IntPtr.Zero)
            {
                return icono;
            }
        }

        return CargarIconoSistema(IdiApplication);
    }

    private const uint LrLoadFromFile = 0x00000010;
    private const uint LrDefaultSize = 0x00000040;
    private const int IdiApplication = 32512;
    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint MF_GRAYED = 0x00000001;

    private delegate IntPtr WndProcDelegado(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ClaseVentana
    {
        public int cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DatosIconoBandeja
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Punto
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref ClaseVentana lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref DatosIconoBandeja lpData);

    private static bool NotificarIconoBandeja(int operacion, ref DatosIconoBandeja datos) =>
        Shell_NotifyIcon(operacion, ref datos);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(
        IntPtr hInst,
        string name,
        uint type,
        int cx,
        int cy,
        uint fuLoad);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, int lpIconName);

    private static IntPtr CargarIconoDesdeArchivo(string ruta, IntPtr hInst, int cx, int cy, uint fuLoad) =>
        LoadImage(hInst, ruta, 1, cx, cy, fuLoad);

    private static IntPtr CargarIconoSistema(int id) =>
        LoadIcon(IntPtr.Zero, id);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, int uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Punto lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(
        IntPtr hmenu,
        uint fuFlags,
        int x,
        int y,
        IntPtr hWnd,
        IntPtr lptpm);

    private static void DestruirIcono(IntPtr handle) => DestroyIcon(handle);

    private static void DestruirVentana(IntPtr handle) => DestroyWindow(handle);

    private static void RegistrarClaseVentana(ref ClaseVentana clase) => RegisterClassEx(ref clase);

    private static IntPtr CrearVentanaEx(
        int estiloExtendido,
        string nombreClase,
        string titulo,
        int estilo,
        int x,
        int y,
        int ancho,
        int alto,
        IntPtr padre,
        IntPtr menu,
        IntPtr instancia,
        IntPtr parametro) =>
        CreateWindowEx(
            estiloExtendido,
            nombreClase,
            titulo,
            estilo,
            x,
            y,
            ancho,
            alto,
            padre,
            menu,
            instancia,
            parametro);

    private static IntPtr CrearMenuPopup() => CreatePopupMenu();

    private static void DestruirMenu(IntPtr menu) => DestroyMenu(menu);

    private static IntPtr ObtenerModuloActual() => GetModuleHandle(null);

    private static void AgregarElementoMenu(IntPtr menu, int id, uint flags, int idComando, string? texto) =>
        AppendMenu(menu, flags, idComando, texto ?? string.Empty);

    private static void ObtenerPosicionCursor(out Punto punto) => GetCursorPos(out punto);

    private static void EstablecerVentanaEnPrimerPlano(IntPtr handle) => SetForegroundWindow(handle);
}
