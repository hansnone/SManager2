using SManager.Core.Modelos;

namespace SManager.Core.Configuracion;

/// <summary>Validaciones fail-fast antes de arrancar el motor.</summary>
public sealed class ValidadorConfiguracion
{
    public ResultadoValidacion Validar(ConfiguracionAplicacion config)
    {
        var errores = new List<string>();
        var paresHabilitados = config.Pares.Where(p => p.Habilitado).ToList();

        if (paresHabilitados.Count == 0)
        {
            errores.Add("La configuración no contiene ningún par habilitado.");
        }

        foreach (var par in paresHabilitados)
        {
            if (string.IsNullOrWhiteSpace(par.RutaOrigen))
            {
                errores.Add($"Par '{par.Nombre}': ruta_origen vacía.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(par.RutaDestino))
            {
                errores.Add($"Par '{par.Nombre}': ruta_destino vacía.");
                continue;
            }

            if (!Directory.Exists(par.RutaOrigen))
            {
                errores.Add($"Par '{par.Nombre}': no existe el origen '{par.RutaOrigen}'.");
            }

            if (!Directory.Exists(par.RutaDestino))
            {
                errores.Add($"Par '{par.Nombre}': no existe el destino '{par.RutaDestino}'.");
            }

            if (RutasSeContienen(par.RutaOrigen, par.RutaDestino))
            {
                errores.Add($"Par '{par.Nombre}': origen y destino no pueden contenerse mutuamente.");
            }
        }

        return new ResultadoValidacion(errores.Count == 0, errores);
    }

    private static bool RutasSeContienen(string rutaA, string rutaB)
    {
        try
        {
            var a = Path.GetFullPath(rutaA.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var b = Path.GetFullPath(rutaB.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            return a.StartsWith(b, StringComparison.OrdinalIgnoreCase)
                || b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

public sealed record ResultadoValidacion(bool Valida, IReadOnlyList<string> Errores);
