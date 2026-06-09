using System.Diagnostics;
using System.Text.Json;
using SManager.Ipc.Modelos;

namespace SManager.Ipc;

/// <summary>Acceso a estado.json, control.json y PID por perfil.</summary>
public sealed class ServicioIpc
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        WriteIndented = false
    };

    public async Task<EstadoPerfil?> LeerEstadoAsync(string nombrePerfil, CancellationToken cancelacion = default)
    {
        var texto = await EscrituraAtomica.LeerTextoConReintentoAsync(
            RutasDatos.ResolverRutaEstado(nombrePerfil), cancelacion).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        return JsonSerializer.Deserialize<EstadoPerfil>(texto, OpcionesJson);
    }

    public async Task PublicarEstadoAsync(EstadoPerfil estado, CancellationToken cancelacion = default)
    {
        estado.ActualizadoUtc = DateTime.UtcNow.ToString("o");
        var json = JsonSerializer.Serialize(estado, OpcionesJson);
        await EscrituraAtomica.EscribirTextoAsync(
            RutasDatos.ObtenerRutaEstado(estado.Perfil), json, cancelacion).ConfigureAwait(false);
    }

    public async Task EnviarComandoAsync(string nombrePerfil, ComandoControl comando, CancellationToken cancelacion = default)
    {
        var control = new ControlPerfil
        {
            Comando = comando switch
            {
                ComandoControl.Apagar => "APAGAR",
                ComandoControl.Recargar => "RECARGAR",
                _ => throw new ArgumentOutOfRangeException(nameof(comando))
            },
            EmitidoUtc = DateTime.UtcNow.ToString("o")
        };

        var json = JsonSerializer.Serialize(control, OpcionesJson);
        await EscrituraAtomica.EscribirTextoAsync(
            RutasDatos.ObtenerRutaControl(nombrePerfil), json, cancelacion).ConfigureAwait(false);
    }

    public async Task<ComandoControl?> LeerComandoPendienteAsync(string nombrePerfil, CancellationToken cancelacion = default)
    {
        var texto = await EscrituraAtomica.LeerTextoConReintentoAsync(
            RutasDatos.ResolverRutaControl(nombrePerfil), cancelacion).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var control = JsonSerializer.Deserialize<ControlPerfil>(texto, OpcionesJson);
        if (control?.Comando is null)
        {
            return null;
        }

        return control.Comando.ToUpperInvariant() switch
        {
            "APAGAR" => ComandoControl.Apagar,
            "RECARGAR" => ComandoControl.Recargar,
            _ => null
        };
    }

    public async Task LimpiarComandoAsync(string nombrePerfil, CancellationToken cancelacion = default)
    {
        var ruta = RutasDatos.ResolverRutaControl(nombrePerfil);
        if (File.Exists(ruta))
        {
            await Task.Run(() => File.Delete(ruta), cancelacion).ConfigureAwait(false);
        }
    }

    public void EscribirPid(string nombrePerfil, int pid)
    {
        File.WriteAllText(RutasDatos.ObtenerRutaPid(nombrePerfil), pid.ToString());
    }

    public void EliminarPid(string nombrePerfil)
    {
        var ruta = RutasDatos.ResolverRutaPid(nombrePerfil);
        if (File.Exists(ruta))
        {
            File.Delete(ruta);
        }
    }

    public bool EstaDemonioEnEjecucion(string nombrePerfil) =>
        ObtenerPidActivo(nombrePerfil) is not null;

    /// <summary>Devuelve el PID si el fichero y el proceso SManager.Host son válidos.</summary>
    public int? ObtenerPidActivo(string nombrePerfil)
    {
        var rutaPid = RutasDatos.ResolverRutaPid(nombrePerfil);
        if (!File.Exists(rutaPid))
        {
            return null;
        }

        if (!int.TryParse(File.ReadAllText(rutaPid).Trim(), out var pid))
        {
            EliminarPid(nombrePerfil);
            return null;
        }

        try
        {
            var proceso = Process.GetProcessById(pid);
            if (proceso.HasExited)
            {
                EliminarPid(nombrePerfil);
                return null;
            }

            if (!EsProcesoDemonio(proceso))
            {
                EliminarPid(nombrePerfil);
                return null;
            }

            return pid;
        }
        catch
        {
            EliminarPid(nombrePerfil);
            return null;
        }
    }

    /// <summary>Último recurso cuando APAGAR no cierra el Host en el tiempo de gracia.</summary>
    public bool TerminarDemonioForzadamente(string nombrePerfil)
    {
        var pid = ObtenerPidActivo(nombrePerfil);
        if (pid is null)
        {
            return true;
        }

        try
        {
            var proceso = Process.GetProcessById(pid.Value);
            if (!proceso.HasExited)
            {
                proceso.Kill(entireProcessTree: true);
                proceso.WaitForExit(5000);
            }
        }
        catch
        {
            return false;
        }
        finally
        {
            EliminarPid(nombrePerfil);
        }

        return ObtenerPidActivo(nombrePerfil) is null;
    }

    public IReadOnlyList<string> ListarPerfiles() => RutasDatos.ListarNombresPerfiles();

    private static bool EsProcesoDemonio(Process proceso)
    {
        try
        {
            return proceso.ProcessName.Equals("SManager.Host", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
