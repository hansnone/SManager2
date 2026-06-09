using System.Diagnostics;

namespace SManager.Core.Motor;

/// <summary>Mide RAM y CPU del proceso actual entre publicaciones de estado IPC.</summary>
public sealed class MuestreadorRecursosProceso
{
    private TimeSpan _ultimoTiempoCpu;
    private DateTime _ultimaMuestraUtc;
    private double _ultimoPorcentajeCpu;

    /// <summary>Devuelve memoria de trabajo y CPU % aproximado desde la última muestra.</summary>
    public (long MemoriaTrabajoBytes, double CpuPorcentaje) Muestrear()
    {
        var proceso = Process.GetCurrentProcess();
        var memoria = proceso.WorkingSet64;
        var cpu = CalcularCpu(proceso);
        return (memoria, cpu);
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
}
