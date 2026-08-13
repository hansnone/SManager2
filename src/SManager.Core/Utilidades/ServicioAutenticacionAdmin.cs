using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SManager.Core.Utilidades;

/// <summary>
/// Servicio de autenticación consciente contra el sistema local de Windows.
/// Muestra el cuadro nativo de credenciales de Windows 11 (credui.dll) y valida
/// que el usuario especificado pertenezca al grupo de Administradores locales.
/// </summary>
public static class ServicioAutenticacionAdmin
{
    private const int LOGON32_LOGON_INTERACTIVE = 2;
    private const int LOGON32_LOGON_NETWORK = 3;
    private const int LOGON32_PROVIDER_DEFAULT = 0;

    private const uint CREDUIWIN_GENERIC = 0x1;
    private const uint CREDUIWIN_CHECKBOX = 0x2;

    private const uint CREDUI_FLAGS_GENERIC_CREDENTIALS = 0x1;
    private const uint CREDUI_FLAGS_DO_NOT_PERSIST = 0x2;
    private const uint CREDUI_FLAGS_ALWAYS_SHOW_UI = 0x8;

    private const int NO_ERROR = 0;
    private const int ERROR_CANCELLED = 1223;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDUI_INFO
    {
        public int cbSize;
        public IntPtr hwndParent;
        public string pszMessageText;
        public string pszCaptionText;
        public IntPtr hbmBanner;
    }

    [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int CredUIPromptForWindowsCredentials(
        ref CREDUI_INFO pUiInfo,
        int dwAuthError,
        ref uint pulAuthPackage,
        IntPtr pvInAuthBuffer,
        uint ulInAuthBufferSize,
        out IntPtr ppvOutAuthBuffer,
        out uint pulOutAuthBufferSize,
        ref bool pfSave,
        uint dwFlags);

    [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredUnPackAuthenticationBuffer(
        uint dwFlags,
        IntPtr pAuthBuffer,
        uint cbAuthBuffer,
        StringBuilder pszUserName,
        ref uint pcchMaxUserName,
        StringBuilder pszDomainName,
        ref uint pcchMaxDomainName,
        StringBuilder pszPassword,
        ref uint pcchMaxPassword);

    [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int CredUIPromptForCredentials(
        ref CREDUI_INFO creditInfo,
        string targetName,
        IntPtr reserved1,
        int authError,
        StringBuilder userName,
        int maxUserName,
        StringBuilder password,
        int maxPassword,
        ref bool save,
        uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LogonUser(
        string lpszUsername,
        string lpszDomain,
        string lpszPassword,
        int dwLogonType,
        int dwLogonProvider,
        out SafeAccessTokenHandle phToken);

    /// <summary>
    /// Muestra el cuadro de diálogo nativo de Seguridad de Windows 11 y valida
    /// si el usuario autenticado pertenece al grupo de Administradores locales.
    /// </summary>
    public static bool SolicitarYValidarCredencialesAdminNativas(IntPtr windowHandle, out string? mensajeResultado)
    {
        mensajeResultado = null;

        var info = new CREDUI_INFO
        {
            cbSize = Marshal.SizeOf(typeof(CREDUI_INFO)),
            hwndParent = windowHandle,
            pszCaptionText = "Seguridad de Windows — SManager 2.0",
            pszMessageText = "Autentícate como Administrador Local para habilitar el modo con borrado en origen.",
            hbmBanner = IntPtr.Zero
        };

        uint authPackage = 0;
        IntPtr outAuthBuffer = IntPtr.Zero;
        uint outAuthBufferSize = 0;
        var save = false;

        // Invocación nativa del cuadro moderno de Seguridad de Windows 11
        var status = CredUIPromptForWindowsCredentials(
            ref info,
            0,
            ref authPackage,
            IntPtr.Zero,
            0,
            out outAuthBuffer,
            out outAuthBufferSize,
            ref save,
            CREDUIWIN_GENERIC);

        if (status == ERROR_CANCELLED)
        {
            mensajeResultado = "Operación cancelada por el usuario.";
            return false;
        }

        if (status == NO_ERROR && outAuthBuffer != IntPtr.Zero)
        {
            try
            {
                var sbUser = new StringBuilder(256);
                uint maxUser = (uint)sbUser.Capacity;
                var sbDomain = new StringBuilder(256);
                uint maxDomain = (uint)sbDomain.Capacity;
                var sbPass = new StringBuilder(256);
                uint maxPass = (uint)sbPass.Capacity;

                if (CredUnPackAuthenticationBuffer(0, outAuthBuffer, outAuthBufferSize, sbUser, ref maxUser, sbDomain, ref maxDomain, sbPass, ref maxPass))
                {
                    var usuario = sbUser.ToString().Trim();
                    var dominio = sbDomain.ToString().Trim();
                    var password = sbPass.ToString();
                    sbPass.Clear();

                    if (ValidarCredencialesAdministradorLocal(usuario, password, dominio, out var errorVal))
                    {
                        var userFull = string.IsNullOrWhiteSpace(dominio) ? usuario : $"{dominio}\\{usuario}";
                        mensajeResultado = $"Autenticación correcta en Seguridad de Windows como administrador local ({userFull}).";
                        return true;
                    }

                    mensajeResultado = errorVal ?? "Las credenciales no pertenecen a un usuario administrador local válido.";
                    return false;
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(outAuthBuffer);
            }
        }

        // Fallback secundario a CredUIPromptForCredentials por compatibilidad si el paquete win11 de UI difiere
        return SolicitarConDialogoGenerico(ref info, out mensajeResultado);
    }

    private static bool SolicitarConDialogoGenerico(ref CREDUI_INFO info, out string? mensajeResultado)
    {
        var sbUser = new StringBuilder(512);
        var sbPass = new StringBuilder(512);
        var save = false;
        var flags = CREDUI_FLAGS_GENERIC_CREDENTIALS | CREDUI_FLAGS_ALWAYS_SHOW_UI | CREDUI_FLAGS_DO_NOT_PERSIST;

        var resultado = CredUIPromptForCredentials(
            ref info,
            "SManager.BorradoEnOrigen",
            IntPtr.Zero,
            0,
            sbUser,
            sbUser.Capacity,
            sbPass,
            sbPass.Capacity,
            ref save,
            flags);

        if (resultado == ERROR_CANCELLED)
        {
            mensajeResultado = "Operación cancelada por el usuario.";
            return false;
        }

        if (resultado != NO_ERROR)
        {
            mensajeResultado = $"Error al solicitar credenciales (Código Windows: {resultado}).";
            return false;
        }

        var fullUser = sbUser.ToString().Trim();
        var password = sbPass.ToString();
        sbPass.Clear();

        if (string.IsNullOrWhiteSpace(fullUser))
        {
            mensajeResultado = "Debes especificar un nombre de usuario válido.";
            return false;
        }

        string usuario;
        string dominio;

        if (fullUser.Contains('\\'))
        {
            var partes = fullUser.Split('\\', 2);
            dominio = partes[0];
            usuario = partes[1];
        }
        else if (fullUser.Contains('@'))
        {
            var partes = fullUser.Split('@', 2);
            usuario = partes[0];
            dominio = partes[1];
        }
        else
        {
            usuario = fullUser;
            dominio = ".";
        }

        if (ValidarCredencialesAdministradorLocal(usuario, password, dominio, out var errorVal))
        {
            mensajeResultado = $"Autenticación correcta como administrador local ({fullUser}).";
            return true;
        }

        mensajeResultado = errorVal ?? "Las credenciales no pertenecen a un usuario administrador local válido.";
        return false;
    }

    /// <summary>
    /// Valida si el usuario y contraseña especificados forman un token de inicio de sesión
    /// con el rol de Administrador de Windows en el equipo local.
    /// </summary>
    public static bool ValidarCredencialesAdministradorLocal(string usuario, string contrasena, string dominio, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(usuario))
        {
            error = "El usuario no puede estar vacío.";
            return false;
        }

        SafeAccessTokenHandle token;
        var exito = LogonUser(
            usuario,
            string.IsNullOrWhiteSpace(dominio) ? "." : dominio,
            contrasena,
            LOGON32_LOGON_INTERACTIVE,
            LOGON32_PROVIDER_DEFAULT,
            out token);

        if (!exito)
        {
            exito = LogonUser(
                usuario,
                string.IsNullOrWhiteSpace(dominio) ? "." : dominio,
                contrasena,
                LOGON32_LOGON_NETWORK,
                LOGON32_PROVIDER_DEFAULT,
                out token);
        }

        if (!exito)
        {
            var errCode = Marshal.GetLastWin32Error();
            error = $"Credenciales incorrectas o inicio de sesión denegado por Windows (Código: {errCode}).";
            return false;
        }

        using (token)
        {
            using (var identity = new WindowsIdentity(token.DangerousGetHandle()))
            {
                var principal = new WindowsPrincipal(identity);
                if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    return true;
                }
            }
        }

        error = $"El usuario '{usuario}' inició sesión pero NO pertenece al grupo de Administradores locales.";
        return false;
    }

    /// <summary>
    /// Determina si el proceso actual de SManager se está ejecutando con privilegios de administrador elevados.
    /// </summary>
    public static bool ProcesoActualEsAdministrador()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
