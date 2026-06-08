using System.Text;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Filtra líneas del log en disco por nombre de par (formato nuevo y legado).</summary>
public static class ServicioFiltradoRegistro
{
    public const string EtiquetaTodosLosPares = "Todos los pares";

    /// <summary>Devuelve solo las líneas del par indicado (incluye mensajes globales del perfil).</summary>
    public static string Filtrar(
        string textoCrudo,
        string? filtroNombrePar,
        IReadOnlyDictionary<string, string> mapaIdANombrePar)
    {
        if (string.IsNullOrWhiteSpace(filtroNombrePar)
            || string.Equals(filtroNombrePar, EtiquetaTodosLosPares, StringComparison.OrdinalIgnoreCase))
        {
            return textoCrudo;
        }

        var resultado = new StringBuilder(textoCrudo.Length);
        foreach (var linea in textoCrudo.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(linea))
            {
                continue;
            }

            if (CoincideLinea(linea, filtroNombrePar, mapaIdANombrePar))
            {
                if (resultado.Length > 0)
                {
                    resultado.AppendLine();
                }

                resultado.Append(linea.TrimEnd('\r'));
            }
        }

        return resultado.ToString();
    }

    private static bool CoincideLinea(
        string linea,
        string nombrePar,
        IReadOnlyDictionary<string, string> mapaIdANombrePar)
    {
        // Formato nuevo: [par:NombreDelPar] o [par:*] para mensajes globales.
        if (linea.Contains($"[par:{nombrePar}]", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (linea.Contains("[par:*]", StringComparison.Ordinal)
            || linea.Contains("[__ALL__]", StringComparison.Ordinal))
        {
            return true;
        }

        // Formato legado: [guid-idPar] en la segunda columna.
        foreach (var (idPar, nombre) in mapaIdANombrePar)
        {
            if (!string.Equals(nombre, nombrePar, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (linea.Contains($"[{idPar}]", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
