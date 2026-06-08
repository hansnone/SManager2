using System.Diagnostics;
using SManager.Ipc;

namespace SManager.Gui.Shared;

/// <summary>Delega start/stop/reload en smanager.exe (CLI).</summary>
public sealed class ControladorDaemon
{
    public async Task<(int Codigo, string Salida, string Error)> EjecutarAsync(string argumentos)
    {
        var rutaCli = ResolvedorEjecutables.ResolverRutaCli();
        if (!File.Exists(rutaCli))
        {
            return (1, string.Empty,
                $"No se encuentra smanager.exe. Compila la solución (dotnet build).\nEn Debug usa src\\SManager.Cli\\bin\\Debug\\net8.0; en Release, herramientas\\ junto a la GUI.\nBuscado en: {rutaCli}");
        }

        var directorioTrabajo = Path.GetDirectoryName(rutaCli) ?? AppContext.BaseDirectory;

        var psi = new ProcessStartInfo
        {
            FileName = rutaCli,
            Arguments = argumentos,
            WorkingDirectory = directorioTrabajo,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var proceso = Process.Start(psi);
        if (proceso is null)
        {
            return (1, string.Empty, "No se pudo iniciar el proceso CLI.");
        }

        var salida = await proceso.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var error = await proceso.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await proceso.WaitForExitAsync().ConfigureAwait(false);
        return (proceso.ExitCode, salida.Trim(), error.Trim());
    }
}
