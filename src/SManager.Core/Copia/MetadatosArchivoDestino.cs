namespace SManager.Core.Copia;

/// <summary>Metadatos mínimos de un archivo en destino para comparación sin I/O repetido.</summary>
public readonly record struct MetadatosArchivoDestino(long Tamano, DateTime MtimeUtc);
