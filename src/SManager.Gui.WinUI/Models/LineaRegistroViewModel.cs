using SManager.Gui.WinUI.Servicios;

namespace SManager.Gui.WinUI.Models;

/// <summary>Una línea del log del demonio, parseada para el visor estructurado.</summary>
public sealed class LineaRegistroViewModel
{
    public string TextoCompleto { get; init; } = string.Empty;

    public string Hora { get; init; } = string.Empty;

    public string Par { get; init; } = string.Empty;

    public string Nivel { get; init; } = string.Empty;

    public string Mensaje { get; init; } = string.Empty;

    /// <summary>Construye la vista a partir de una línea en disco o legado sin parsear.</summary>
    public static LineaRegistroViewModel DesdeTexto(string linea)
    {
        if (ServicioAnalisisRegistro.TryParsearLinea(linea, out var hora, out var par, out var nivel, out var mensaje))
        {
            return new LineaRegistroViewModel
            {
                TextoCompleto = linea,
                Hora = hora,
                Par = par,
                Nivel = nivel,
                Mensaje = mensaje
            };
        }

        return new LineaRegistroViewModel
        {
            TextoCompleto = linea,
            Nivel = "—",
            Mensaje = linea
        };
    }
}
