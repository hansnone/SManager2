using SManager.Core.Configuracion;
using SManager.Core.Motor;
using SManager.Ipc;

namespace SManager.Host.Servicios;

/// <summary>
/// Worker de un perfil: arranca el motor y lo mantiene vivo hasta cancelación o APAGAR.
/// </summary>
public sealed class DemonioPerfilWorker : BackgroundService
{
    private readonly OpcionesArranque _opciones;
    private readonly ILogger<DemonioPerfilWorker> _logger;
    private readonly ILoggerFactory _fabricaLogs;
    private MotorSincronizacion? _motor;

    public DemonioPerfilWorker(OpcionesArranque opciones, ILogger<DemonioPerfilWorker> logger, ILoggerFactory fabricaLogs)
    {
        _opciones = opciones;
        _logger = logger;
        _fabricaLogs = fabricaLogs;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_opciones.RutaConfiguracion))
        {
            _logger.LogError("Modo demonio requiere --config con la ruta al JSON.");
            return;
        }

        var rutaConfig = Path.GetFullPath(_opciones.RutaConfiguracion);
        var opcionesMotor = new OpcionesDemonio
        {
            NombrePerfil = _opciones.Perfil,
            RutaConfiguracion = rutaConfig
        };

        _motor = new MotorSincronizacion(
            opcionesMotor,
            new ConfiguracionRepositorio(),
            new ValidadorConfiguracion(),
            new ServicioIpc(),
            _fabricaLogs.CreateLogger<MotorSincronizacion>());

        try
        {
            await _motor.IniciarAsync(stoppingToken).ConfigureAwait(false);

            // El bucle del motor termina solo al recibir APAGAR o cancelación del host.
            if (_motor.TareaPrincipal is not null)
            {
                await _motor.TareaPrincipal.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Apagado normal.
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fallo fatal en demonio del perfil {Perfil}", _opciones.Perfil);
            throw;
        }
        finally
        {
            if (_motor is not null)
            {
                await _motor.DetenerOrdenadoAsync(CancellationToken.None).ConfigureAwait(false);
                await _motor.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
