namespace SManager.Core.Vigia;

/// <summary>Estado de seguimiento de estabilidad de un archivo candidato a copia.</summary>
internal sealed class SeguimientoEstabilidad
{
    public long Tamano { get; set; } = -1;
    public DateTime MtimeUtc { get; set; } = DateTime.MinValue;
    public DateTime? EstableDesdeUtc { get; set; }
}
