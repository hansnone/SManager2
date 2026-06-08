using System.IO.Enumeration;
using SManager.Core.Modelos;

namespace SManager.Core.Utilidades;

/// <summary>Filtros de inclusión/exclusión compatibles con patrones -like de PowerShell.</summary>
public static class ServicioFiltros
{
    public static bool PasaFiltros(string nombreArchivo, ParSincronizacion par)
    {
        var inclusiones = DividirPatrones(par.FiltroInclusion);
        var exclusiones = DividirPatrones(par.FiltroExclusion);

        if (inclusiones.Count > 0 && !CoincideAlguno(nombreArchivo, inclusiones))
        {
            return false;
        }

        if (exclusiones.Count > 0 && CoincideAlguno(nombreArchivo, exclusiones))
        {
            return false;
        }

        return true;
    }

    public static IReadOnlyList<string> DividirPatrones(string? cadena)
    {
        if (string.IsNullOrWhiteSpace(cadena))
        {
            return [];
        }

        return cadena.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool CoincideAlguno(string nombreArchivo, IReadOnlyList<string> patrones)
    {
        foreach (var patron in patrones)
        {
            if (string.IsNullOrWhiteSpace(patron))
            {
                continue;
            }

            if (FileSystemName.MatchesSimpleExpression(patron.Trim(), nombreArchivo, ignoreCase: true))
            {
                return true;
            }
        }

        return false;
    }
}
