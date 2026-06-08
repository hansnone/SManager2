namespace SManager.Core.Utilidades;

/// <summary>Detección de archivos OneDrive solo en la nube.</summary>
public static class OneDrivePlaceholder
{
    private const FileAttributes Offline = (FileAttributes)0x1000;
    private const FileAttributes RecallOnOpen = (FileAttributes)0x40000;
    private const FileAttributes RecallOnDataAccess = (FileAttributes)0x400000;

    public static bool EsPlaceholder(FileAttributes atributos) =>
        atributos.HasFlag(Offline)
        || atributos.HasFlag(RecallOnOpen)
        || atributos.HasFlag(RecallOnDataAccess);

    public static bool EsPlaceholder(string ruta)
    {
        try
        {
            var info = new FileInfo(ruta);
            return info.Exists && !info.Attributes.HasFlag(FileAttributes.Directory) && EsPlaceholder(info.Attributes);
        }
        catch
        {
            return false;
        }
    }
}
