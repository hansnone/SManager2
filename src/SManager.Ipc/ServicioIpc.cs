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
            RutasDatos.ObtenerRutaEstado(nombrePerfil), cancelacion).ConfigureAwait(false);

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
            RutasDatos.ObtenerRutaControl(nombrePerfil), cancelacion).ConfigureAwait(false);

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
        var ruta = RutasDatos.ObtenerRutaControl(nombrePerfil);
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
        var ruta = RutasDatos.ObtenerRutaPid(nombrePerfil);
        if (File.Exists(ruta))
        {
            File.Delete(ruta);
        }
    }

    public bool EstaDemonioEnEjecucion(string nombrePerfil)
    {
        var rutaPid = RutasDatos.ObtenerRutaPid(nombrePerfil);
        if (!File.Exists(rutaPid))
        {
            return false;
        }

        if (!int.TryParse(File.ReadAllText(rutaPid).Trim(), out var pid))
        {
            return false;
        }

        try
        {
            var proceso = Process.GetProcessById(pid);
            return !proceso.HasExited;
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<string> ListarPerfiles() => RutasDatos.ListarNombresPerfiles();
}
