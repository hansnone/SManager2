using System.Text.RegularExpressions;
using SManager.Gui.WinUI.Models;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Parsea y filtra líneas del log en disco para el visor estructurado.</summary>
public static partial class ServicioAnalisisRegistro
{
    // Formato: [2026-06-08 12:00:00] [perfil:X] [par:Y] [NIVEL] mensaje
    [GeneratedRegex(
        @"^\[(?<hora>[^\]]+)\]\s+\[perfil:[^\]]+\]\s+\[par:(?<par>[^\]]+)\]\s+\[(?<nivel>[^\]]+)\]\s+(?<mensaje>.*)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex RegexLineaEstructurada();

    /// <summary>Intenta extraer hora, par, nivel y mensaje de una línea del demonio.</summary>
    public static bool TryParsearLinea(
        string linea,
        out string hora,
        out string par,
        out string nivel,
        out string mensaje)
    {
        hora = par = nivel = mensaje = string.Empty;
        var coincidencia = RegexLineaEstructurada().Match(linea.TrimEnd('\r'));
        if (!coincidencia.Success)
        {
            return false;
        }

        hora = coincidencia.Groups["hora"].Value;
        par = coincidencia.Groups["par"].Value;
        nivel = coincidencia.Groups["nivel"].Value;
        mensaje = coincidencia.Groups["mensaje"].Value;
        return true;
    }

    /// <summary>Convierte texto crudo del log en entradas filtradas por par, nivel y búsqueda.</summary>
    public static IReadOnlyList<LineaRegistroViewModel> Procesar(
        string textoCrudo,
        string? filtroNombrePar,
        string? filtroNivel,
        string? textoBusqueda,
        IReadOnlyDictionary<string, string> mapaIdANombrePar)
    {
        var resultado = new List<LineaRegistroViewModel>();
        foreach (var linea in textoCrudo.Split('\n'))
        {
            var limpia = linea.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(limpia))
            {
                continue;
            }

            if (!PasaFiltroPar(limpia, filtroNombrePar, mapaIdANombrePar))
            {
                continue;
            }

            var entrada = LineaRegistroViewModel.DesdeTexto(limpia);
            if (!PasaFiltroNivel(entrada.Nivel, filtroNivel))
            {
                continue;
            }

            if (!PasaBusqueda(entrada, limpia, textoBusqueda))
            {
                continue;
            }

            resultado.Add(entrada);
        }

        return resultado;
    }

    /// <summary>Procesa solo líneas nuevas del texto crudo y las añade a la colección visible.</summary>
    public static int AnexarLineasFiltradas(
        IReadOnlyList<string> lineasCrudas,
        int indicePrimeraLineaNueva,
        string? filtroNombrePar,
        string? filtroNivel,
        string? textoBusqueda,
        IReadOnlyDictionary<string, string> mapaIdANombrePar,
        ICollection<LineaRegistroViewModel> destino)
    {
        var anexadas = 0;
        for (var i = indicePrimeraLineaNueva; i < lineasCrudas.Count; i++)
        {
            var limpia = lineasCrudas[i].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(limpia))
            {
                continue;
            }

            if (!PasaFiltroPar(limpia, filtroNombrePar, mapaIdANombrePar))
            {
                continue;
            }

            var entrada = LineaRegistroViewModel.DesdeTexto(limpia);
            if (!PasaFiltroNivel(entrada.Nivel, filtroNivel))
            {
                continue;
            }

            if (!PasaBusqueda(entrada, limpia, textoBusqueda))
            {
                continue;
            }

            destino.Add(entrada);
            anexadas++;
        }

        return anexadas;
    }

    /// <summary>Divide el texto crudo del log en líneas sin descartar la última incompleta.</summary>
    public static string[] DividirLineasCrudas(string textoCrudo) =>
        string.IsNullOrEmpty(textoCrudo)
            ? Array.Empty<string>()
            : textoCrudo.Split('\n');

    private static bool PasaFiltroPar(
        string linea,
        string? filtroNombrePar,
        IReadOnlyDictionary<string, string> mapaIdANombrePar)
    {
        if (string.IsNullOrWhiteSpace(filtroNombrePar)
            || string.Equals(filtroNombrePar, ServicioFiltradoRegistro.EtiquetaTodosLosPares, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ServicioFiltradoRegistro.CoincideLineaPublica(linea, filtroNombrePar, mapaIdANombrePar);
    }

    private static bool PasaFiltroNivel(string nivelLinea, string? filtroNivel)
    {
        if (string.IsNullOrWhiteSpace(filtroNivel)
            || string.Equals(filtroNivel, MapeadorNivelRegistro.EtiquetaTodosLosNiveles, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(nivelLinea, filtroNivel, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PasaBusqueda(LineaRegistroViewModel entrada, string lineaCompleta, string? textoBusqueda)
    {
        if (string.IsNullOrWhiteSpace(textoBusqueda))
        {
            return true;
        }

        return lineaCompleta.Contains(textoBusqueda, StringComparison.OrdinalIgnoreCase)
               || entrada.Mensaje.Contains(textoBusqueda, StringComparison.OrdinalIgnoreCase)
               || entrada.Par.Contains(textoBusqueda, StringComparison.OrdinalIgnoreCase)
               || entrada.Nivel.Contains(textoBusqueda, StringComparison.OrdinalIgnoreCase);
    }
}
