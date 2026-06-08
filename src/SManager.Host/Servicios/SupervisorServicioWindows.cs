using SManager.Ipc;

namespace SManager.Host.Servicios;

/// <summary>
/// Worker del Windows Service: vigila perfiles con PID activo y publica salud del servicio.
/// Los motores por perfil se lanzan vía CLI (smanager start); el servicio garantiza
/// persistencia del proceso host y punto de instalación SCM profesional.
/// </summary>
public sealed class SupervisorServicioWindows : BackgroundService
{
    private readonly ServicioIpc _ipc = new();
    private readonly ILogger<SupervisorServicioWindows> _logger;

    public SupervisorServicioWindows(ILogger<SupervisorServicioWindows> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SManager 2.0 servicio iniciado. Datos en {Raiz}",
            RutasDatos.ObtenerRaiz());

        while (!stoppingToken.IsCancellationRequested)
        {
            var perfiles = _ipc.ListarPerfiles();
            var activos = perfiles.Count(p => _ipc.EstaDemonioEnEjecucion(p));

            if (activos > 0)
            {
                _logger.LogDebug("Supervisor: {Activos} perfil(es) con demonio activo", activos);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("SManager 2.0 servicio detenido.");
    }
}
