using System.Diagnostics;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>
/// Mide RAM y CPU de un proceso externo (p. ej. SManager.Host) cuando el estado IPC
/// aún no trae el bloque <c>recursos</c> (demonio compilado antes de la telemetría nueva).
/// </summary>
public sealed class MuestreadorRecursosPorPid
{
    private int _pidMuestreado;
    private TimeSpan _ultimoTiempoCpu;
    private DateTime _ultimaMuestraUtc;
    private double _ultimoPorcentajeCpu;

    /// <summary>Devuelve memoria de trabajo y CPU % si el PID sigue vivo y es SManager.Host.</summary>
    public (long MemoriaTrabajoBytes, double CpuPorcentaje)? Muestrear(int pid)
    {
        if (pid <= 0)
        {
            return null;
        }

        if (pid != _pidMuestreado)
        {
            ReiniciarEstado(pid);
        }

        try
        {
            var proceso = Process.GetProcessById(pid);
            if (proceso.HasExited || !EsProcesoDemonio(proceso))
            {
                return null;
            }

            var memoria = proceso.WorkingSet64;
            var cpu = CalcularCpu(proceso);
            return (memoria, cpu);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Hora UTC de arranque del proceso; sirve de respaldo para inicio de sesión.</summary>
    public DateTimeOffset? ObtenerInicioUtc(int pid)
    {
        if (pid <= 0)
        {
            return null;
        }

        try
        {
            var proceso = Process.GetProcessById(pid);
            if (proceso.HasExited || !EsProcesoDemonio(proceso))
            {
                return null;
            }

            return proceso.StartTime.ToUniversalTime();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Descarta muestras previas al cambiar de PID o de perfil.</summary>
    public void Reiniciar()
    {
        _pidMuestreado = 0;
        _ultimoTiempoCpu = default;
        _ultimaMuestraUtc = default;
        _ultimoPorcentajeCpu = 0;
    }

    private void ReiniciarEstado(int pid)
    {
        _pidMuestreado = pid;
        _ultimoTiempoCpu = default;
        _ultimaMuestraUtc = default;
        _ultimoPorcentajeCpu = 0;
    }

    private double CalcularCpu(Process proceso)
    {
        var ahora = DateTime.UtcNow;
        var tiempoCpu = proceso.TotalProcessorTime;

        if (_ultimaMuestraUtc == default)
        {
            _ultimoTiempoCpu = tiempoCpu;
            _ultimaMuestraUtc = ahora;
            return 0;
        }

        var transcurridoMs = (ahora - _ultimaMuestraUtc).TotalMilliseconds;
        if (transcurridoMs < 50)
        {
            return _ultimoPorcentajeCpu;
        }

        var cpuUsadoMs = (tiempoCpu - _ultimoTiempoCpu).TotalMilliseconds;
        var nucleos = Math.Max(1, Environment.ProcessorCount);
        _ultimoPorcentajeCpu = Math.Clamp(cpuUsadoMs / (transcurridoMs * nucleos) * 100.0, 0, 100);

        _ultimoTiempoCpu = tiempoCpu;
        _ultimaMuestraUtc = ahora;
        return _ultimoPorcentajeCpu;
    }

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
