using Microsoft.Win32;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>
/// Gestiona la entrada de auto-arranque en HKCU\...\Run (sin privilegios de administrador).
/// </summary>
public static class ServicioAutoInicioSistema
{
    private const string SubclaveRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string NombreEntrada = "SManager2";
    private const string ArgumentoMinimizado = "-minimized";

    /// <summary>Estado actual leído del registro de Windows.</summary>
    public readonly record struct EstadoAutoInicio(bool Habilitado, bool Minimizado);

    /// <summary>Lee si SManager está registrado para iniciar con Windows.</summary>
    public static EstadoAutoInicio LeerEstado()
    {
        try
        {
            using var clave = Registry.CurrentUser.OpenSubKey(SubclaveRun, writable: false);
            var valor = clave?.GetValue(NombreEntrada) as string;
            if (string.IsNullOrWhiteSpace(valor))
            {
                return new EstadoAutoInicio(false, true);
            }

            var minimizado = valor.Contains(ArgumentoMinimizado, StringComparison.OrdinalIgnoreCase);
            return new EstadoAutoInicio(true, minimizado);
        }
        catch
        {
            return new EstadoAutoInicio(false, true);
        }
    }

    /// <summary>Registra o elimina la entrada Run según las preferencias del usuario.</summary>
    public static void Aplicar(bool habilitado, bool minimizado)
    {
        try
        {
            using var clave = Registry.CurrentUser.OpenSubKey(SubclaveRun, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(SubclaveRun, writable: true);

            if (!habilitado)
            {
                clave.DeleteValue(NombreEntrada, throwOnMissingValue: false);
                return;
            }

            var rutaEjecutable = Environment.ProcessPath
                ?? Path.Combine(AppContext.BaseDirectory, "SManager.Gui.WinUI.exe");

            var argumentos = minimizado ? $" {ArgumentoMinimizado}" : string.Empty;
            var valor = $"\"{rutaEjecutable}\"{argumentos}";
            clave.SetValue(NombreEntrada, valor);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "No se pudo actualizar el auto-arranque en el registro de Windows.", ex);
        }
    }
}
