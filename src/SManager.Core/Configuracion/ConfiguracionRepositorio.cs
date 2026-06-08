using System.Text.Json;
using SManager.Core.Modelos;

namespace SManager.Core.Configuracion;

/// <summary>Lectura y normalización del JSON de configuración.</summary>
public sealed class ConfiguracionRepositorio
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public async Task<ConfiguracionAplicacion> LeerAsync(string rutaConfig, CancellationToken cancelacion = default)
    {
        if (!File.Exists(rutaConfig))
        {
            throw new FileNotFoundException($"No existe el archivo de configuración: {rutaConfig}");
        }

        await using var flujo = File.OpenRead(rutaConfig);
        var config = await JsonSerializer.DeserializeAsync<ConfiguracionAplicacion>(flujo, OpcionesJson, cancelacion)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException("El JSON de configuración está vacío o es inválido.");

        Normalizar(config);
        return config;
    }

    public async Task GuardarAsync(string rutaConfig, ConfiguracionAplicacion config, CancellationToken cancelacion = default)
    {
        Normalizar(config);
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var directorio = Path.GetDirectoryName(rutaConfig);
        if (!string.IsNullOrEmpty(directorio))
        {
            Directory.CreateDirectory(directorio);
        }

        await File.WriteAllTextAsync(rutaConfig, json, cancelacion).ConfigureAwait(false);
    }

    public static void Normalizar(ConfiguracionAplicacion config)
    {
        config.IntervaloPollingSegundos = Math.Clamp(config.IntervaloPollingSegundos, 30, 3600);
        config.SegundosEstabilidadArchivo = Math.Clamp(config.SegundosEstabilidadArchivo, 1, 30);
        config.NumCopiadoresParalelos = Math.Clamp(config.NumCopiadoresParalelos, 1, 32);
        config.NumHidratadoresParalelos = Math.Clamp(config.NumHidratadoresParalelos, 1, 16);
        config.IntervaloPublicacionEstadoMs = Math.Max(200, config.IntervaloPublicacionEstadoMs);

        foreach (var par in config.Pares)
        {
            if (string.IsNullOrWhiteSpace(par.IdPar))
            {
                par.IdPar = Guid.NewGuid().ToString();
            }
        }
    }
}
