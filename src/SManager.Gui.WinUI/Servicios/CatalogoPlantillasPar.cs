using SManager.Gui.WinUI.Models;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Ejemplos preconfigurados que el asistente ofrece al usuario.</summary>
public static class CatalogoPlantillasPar
{
    public static IReadOnlyList<PlantillaParEjemplo> ObtenerPlantillas() =>
    [
        new()
        {
            Id = "fotos_externo",
            Titulo = "Fotos → disco externo",
            Descripcion =
                "Copia imágenes y vídeos desde tu carpeta de fotos hacia un disco USB o unidad externa. "
                + "Excluye archivos temporales del sistema.",
            NombreParSugerido = "Backup fotos",
            FiltroInclusion = "*.jpg;*.jpeg;*.png;*.heic;*.mp4;*.mov",
            FiltroExclusion = "~$*;*.tmp;Thumbs.db;desktop.ini",
            PistaDestino = "Ejemplo: D:\\Backup\\Fotos o E:\\Copia\\Fotos"
        },
        new()
        {
            Id = "documentos_nas",
            Titulo = "Documentos → NAS o red",
            Descripcion =
                "Mantiene una copia de documentos de trabajo en una carpeta de red o NAS. "
                + "Incluye PDF, Office y texto plano.",
            NombreParSugerido = "Documentos NAS",
            FiltroInclusion = "*.pdf;*.docx;*.xlsx;*.pptx;*.txt;*.md",
            FiltroExclusion = "~$*;*.tmp;*.partial;*.lnk",
            PistaDestino = "Ejemplo: \\\\servidor\\compartido\\Documentos"
        },
        new()
        {
            Id = "trabajo_backup",
            Titulo = "Carpeta de trabajo → backup local",
            Descripcion =
                "Replica una carpeta de proyecto hacia otra ubicación en el mismo PC o disco secundario. "
                + "Copia todo excepto temporales habituales.",
            NombreParSugerido = "Copia trabajo",
            FiltroInclusion = "*",
            FiltroExclusion = "~$*;*.tmp;*.partial;*.lnk;node_modules;*.log",
            PistaDestino = "Ejemplo: D:\\Backups\\Proyecto"
        },
        new()
        {
            Id = "personalizado",
            Titulo = "Personalizado",
            Descripcion =
                "Tú eliges origen, destino y filtros paso a paso. Recomendado si ya sabes qué carpetas quieres sincronizar.",
            NombreParSugerido = "Mi sincronización",
            FiltroInclusion = "*",
            FiltroExclusion = "~$*;*.tmp;*.partial;*.lnk"
        }
    ];

    public static PlantillaParEjemplo? BuscarPorId(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : ObtenerPlantillas().FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
