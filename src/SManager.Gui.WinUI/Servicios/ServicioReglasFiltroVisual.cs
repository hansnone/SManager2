using SManager.Core.Modelos;
using SManager.Core.Utilidades;
using SManager.Gui.WinUI.Models;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Convierte entre cadenas de exclusión y reglas visuales editables.</summary>
public static class ServicioReglasFiltroVisual
{
    /// <summary>Parsea la cadena de exclusión del JSON en reglas individuales.</summary>
    public static List<ReglaFiltroViewModel> DesdeCadenaExclusion(string? cadena)
    {
        var reglas = new List<ReglaFiltroViewModel>();
        foreach (var patron in ServicioFiltros.DividirPatrones(cadena))
        {
            if (string.IsNullOrWhiteSpace(patron))
            {
                continue;
            }

            var tipo = patron.Contains('*') || patron.Contains('?')
                ? TipoReglaFiltro.PatronAvanzado
                : TipoReglaFiltro.Extension;

            reglas.Add(new ReglaFiltroViewModel
            {
                Tipo = tipo,
                Patron = patron.Trim()
            });
        }

        if (reglas.Count == 0)
        {
            reglas.Add(new ReglaFiltroViewModel { Patron = "*.tmp" });
        }

        return reglas;
    }

    /// <summary>Serializa las reglas visuales a la cadena guardada en configuracion.json.</summary>
    public static string HaciaCadenaExclusion(IEnumerable<ReglaFiltroViewModel> reglas)
    {
        var patrones = reglas
            .Select(r => r.Patron.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return string.Join(';', patrones);
    }

    /// <summary>Cuenta cuántos archivos pasarían o no los filtros actuales en una carpeta.</summary>
    public static ResultadoPruebaFiltros ProbarCarpeta(string rutaCarpeta, ParSincronizacion par)
    {
        var resultado = new ResultadoPruebaFiltros();
        if (!Directory.Exists(rutaCarpeta))
        {
            return resultado;
        }

        var revisados = 0;
        var copiados = 0;
        var omitidos = 0;

        foreach (var ruta in Directory.EnumerateFiles(rutaCarpeta, "*", SearchOption.AllDirectories))
        {
            revisados++;
            try
            {
                var nombre = Path.GetFileName(ruta);
                if (ServicioFiltros.PasaFiltros(nombre, par))
                {
                    copiados++;
                }
                else
                {
                    omitidos++;
                }
            }
            catch
            {
                omitidos++;
            }

            if (revisados >= 50_000)
            {
                break;
            }
        }

        return new ResultadoPruebaFiltros
        {
            ArchivosRevisados = revisados,
            ArchivosCopiados = copiados,
            ArchivosOmitidos = omitidos
        };
    }
}
