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
    private const string ArgumentoAutostartDemonio = "-autostart-daemon";

    /// <summary>Estado actual leído del registro de Windows.</summary>
    public readonly record struct EstadoAutoInicio(bool Habilitado, bool Minimizado, bool IniciarDemonio);

    /// <summary>Lee si SManager está registrado para iniciar con Windows.</summary>
    public static EstadoAutoInicio LeerEstado()
    {
        try
        {
            using var clave = Registry.CurrentUser.OpenSubKey(SubclaveRun, writable: false);
            var valor = clave?.GetValue(NombreEntrada) as string;
            if (string.IsNullOrWhiteSpace(valor))
            {
                return new EstadoAutoInicio(false, true, true);
            }

            var minimizado = valor.Contains(ArgumentoMinimizado, StringComparison.OrdinalIgnoreCase);
            var iniciarDemonio = valor.Contains(ArgumentoAutostartDemonio, StringComparison.OrdinalIgnoreCase);
            return new EstadoAutoInicio(true, minimizado, iniciarDemonio);
        }
        catch
        {
            return new EstadoAutoInicio(false, true, true);
        }
    }

    /// <summary>Registra o elimina la entrada Run según las preferencias del usuario.</summary>
    public static void Aplicar(bool habilitado, bool minimizado, bool iniciarDemonio)
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

            var argumentos = ConstruirArgumentosArranque(minimizado, iniciarDemonio);
            var valor = $"\"{rutaEjecutable}\"{argumentos}";
            clave.SetValue(NombreEntrada, valor);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "No se pudo actualizar el auto-arranque en el registro de Windows.", ex);
        }
    }

    private static string ConstruirArgumentosArranque(bool minimizado, bool iniciarDemonio)
    {
        var partes = new List<string>(2);
        if (minimizado)
        {
            partes.Add(ArgumentoMinimizado);
        }

        if (iniciarDemonio)
        {
            partes.Add(ArgumentoAutostartDemonio);
        }

        return partes.Count == 0 ? string.Empty : " " + string.Join(" ", partes);
    }
}
