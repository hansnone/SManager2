using SManager.Core.Modelos;

namespace SManager.Gui.Shared.Servicios;

/// <summary>Detecta configuraciones de par que pueden confundir o dañar datos.</summary>
public static class ServicioValidacionRiesgoPar
{
    /// <summary>Devuelve avisos legibles para mostrar en tarjetas o antes de sincronizar.</summary>
    public static IReadOnlyList<string> DetectarAvisos(ParSincronizacion par)
    {
        var avisos = new List<string>();

        if (string.IsNullOrWhiteSpace(par.RutaOrigen) || string.IsNullOrWhiteSpace(par.RutaDestino))
        {
            return avisos;
        }

        try
        {
            var origen = Path.GetFullPath(
                par.RutaOrigen.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var destino = Path.GetFullPath(
                par.RutaDestino.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            if (origen.Equals(destino, StringComparison.OrdinalIgnoreCase))
            {
                avisos.Add("Origen y destino son la misma carpeta. No se copiará nada.");
            }

            var separadorOrigen = origen + Path.DirectorySeparatorChar;
            var separadorOrigenAlt = origen + Path.AltDirectorySeparatorChar;
            if (destino.StartsWith(separadorOrigen, StringComparison.OrdinalIgnoreCase)
                || destino.StartsWith(separadorOrigenAlt, StringComparison.OrdinalIgnoreCase))
            {
                avisos.Add("El destino está dentro del origen. Puede provocar copias recursivas.");
            }

            if (!Directory.Exists(origen))
            {
                avisos.Add("La carpeta origen no está disponible o no existe.");
            }

            if (!Directory.Exists(destino))
            {
                avisos.Add("La carpeta destino no está disponible o no existe.");
            }
            else if (DirectorioTieneContenido(destino))
            {
                avisos.Add(
                    "El destino ya contiene archivos. Los existentes con el mismo nombre pueden sobrescribirse.");
            }
        }
        catch
        {
            avisos.Add("No se pudieron validar las rutas del par.");
        }

        return avisos;
    }

    private static bool DirectorioTieneContenido(string ruta)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(ruta).Any();
        }
        catch
        {
            return false;
        }
    }
}
