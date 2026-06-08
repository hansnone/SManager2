using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SManager.Core.Configuracion;
using SManager.Core.Modelos;
using SManager.Core.Motor;
using SManager.Ipc;

namespace SManager.Core.Tests;

/// <summary>Pruebas de integración del motor contra el sistema de ficheros real.</summary>
public sealed class MotorIntegracionTests : IAsyncLifetime
{
    private readonly string _perfil = $"Test_{Guid.NewGuid():N}";
    private string _directorioBase = string.Empty;
    private string _origen = string.Empty;
    private string _destino = string.Empty;
    private string _rutaConfig = string.Empty;

    public Task InitializeAsync()
    {
        _directorioBase = Path.Combine(Path.GetTempPath(), "SManager2Tests", Guid.NewGuid().ToString("N"));
        _origen = Path.Combine(_directorioBase, "origen");
        _destino = Path.Combine(_directorioBase, "destino");
        Directory.CreateDirectory(_origen);
        Directory.CreateDirectory(_destino);

        _rutaConfig = Path.Combine(RutasDatos.ObtenerCarpetaPerfil(_perfil), "configuracion.json");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        try
        {
            if (Directory.Exists(_directorioBase))
            {
                Directory.Delete(_directorioBase, recursive: true);
            }

            var carpetaPerfil = RutasDatos.ObtenerCarpetaPerfil(_perfil);
            if (Directory.Exists(carpetaPerfil))
            {
                Directory.Delete(carpetaPerfil, recursive: true);
            }
        }
        catch
        {
            // Limpieza best-effort en entorno compartido.
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Copia_ArchivoNuevo_HaciaDestino()
    {
        await GuardarConfiguracionAsync(CrearConfig()).ConfigureAwait(false);

        await using var motor = CrearMotor();
        await motor.IniciarAsync(CancellationToken.None).ConfigureAwait(false);

        var archivoOrigen = Path.Combine(_origen, "prueba.txt");
        await File.WriteAllTextAsync(archivoOrigen, "contenido de prueba").ConfigureAwait(false);

        var archivoDestino = Path.Combine(_destino, "prueba.txt");
        var copiado = await EsperarCondicionAsync(
            () => File.Exists(archivoDestino),
            TimeSpan.FromSeconds(15)).ConfigureAwait(false);

        Assert.True(copiado, "El archivo no se copió al destino dentro del tiempo esperado.");
        Assert.Equal(await File.ReadAllTextAsync(archivoOrigen).ConfigureAwait(false),
            await File.ReadAllTextAsync(archivoDestino).ConfigureAwait(false));

        await motor.DetenerOrdenadoAsync(CancellationToken.None).ConfigureAwait(false);
    }

    [Fact]
    public async Task RecargaEnCaliente_RechazaConfigInvalida()
    {
        await GuardarConfiguracionAsync(CrearConfig()).ConfigureAwait(false);

        await using var motor = CrearMotor();
        await motor.IniciarAsync(CancellationToken.None).ConfigureAwait(false);

        var configInvalida = CrearConfig();
        configInvalida.Pares[0].RutaDestino = Path.Combine(_directorioBase, "no_existe");
        await GuardarConfiguracionAsync(configInvalida).ConfigureAwait(false);

        var ipc = new ServicioIpc();
        await ipc.EnviarComandoAsync(_perfil, ComandoControl.Recargar).ConfigureAwait(false);
        await Task.Delay(3000).ConfigureAwait(false);

        var estado = await ipc.LeerEstadoAsync(_perfil).ConfigureAwait(false);
        Assert.NotNull(estado);
        Assert.True(estado!.EnEjecucion);

        var configActual = JsonSerializer.Deserialize<ConfiguracionAplicacion>(
            await File.ReadAllTextAsync(RutasDatos.ObtenerRutaConfiguracionActiva(_perfil)).ConfigureAwait(false))!;
        Assert.True(Directory.Exists(configActual.Pares[0].RutaDestino));

        await motor.DetenerOrdenadoAsync(CancellationToken.None).ConfigureAwait(false);
    }

    [Fact]
    public async Task ApagadoOrdenado_EliminaPidYLimpiaControl()
    {
        await GuardarConfiguracionAsync(CrearConfig()).ConfigureAwait(false);

        await using var motor = CrearMotor();
        await motor.IniciarAsync(CancellationToken.None).ConfigureAwait(false);

        var ipc = new ServicioIpc();
        Assert.True(ipc.EstaDemonioEnEjecucion(_perfil));

        await ipc.EnviarComandoAsync(_perfil, ComandoControl.Apagar).ConfigureAwait(false);

        var detenido = await EsperarCondicionAsync(
            () => !ipc.EstaDemonioEnEjecucion(_perfil),
            TimeSpan.FromSeconds(30)).ConfigureAwait(false);

        Assert.True(detenido, "El demonio no se detuvo tras la señal APAGAR.");
        Assert.False(File.Exists(RutasDatos.ObtenerRutaPid(_perfil)));

        if (motor.TareaPrincipal is not null)
        {
            await motor.TareaPrincipal.ConfigureAwait(false);
        }
    }

    private MotorSincronizacion CrearMotor() => new(
        new OpcionesDemonio { NombrePerfil = _perfil, RutaConfiguracion = _rutaConfig },
        new ConfiguracionRepositorio(),
        new ValidadorConfiguracion(),
        new ServicioIpc(),
        NullLogger<MotorSincronizacion>.Instance);

    private ConfiguracionAplicacion CrearConfig() => new()
    {
        IntervaloPollingSegundos = 3600,
        SegundosEstabilidadArchivo = 1,
        NumCopiadoresParalelos = 2,
        NumHidratadoresParalelos = 1,
        IntervaloPublicacionEstadoMs = 200,
        Pares =
        [
            new ParSincronizacion
            {
                IdPar = "par-test",
                Nombre = "Test",
                Habilitado = true,
                RutaOrigen = _origen,
                RutaDestino = _destino,
                FiltroInclusion = "*"
            }
        ]
    };

    private async Task GuardarConfiguracionAsync(ConfiguracionAplicacion config)
    {
        var repo = new ConfiguracionRepositorio();
        await repo.GuardarAsync(_rutaConfig, config).ConfigureAwait(false);
    }

    private static async Task<bool> EsperarCondicionAsync(Func<bool> condicion, TimeSpan timeout)
    {
        var limite = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < limite)
        {
            if (condicion())
            {
                return true;
            }

            await Task.Delay(200).ConfigureAwait(false);
        }

        return condicion();
    }
}
