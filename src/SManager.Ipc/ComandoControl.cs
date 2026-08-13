namespace SManager.Ipc;

/// <summary>Señales que la CLI/GUI envían al demonio vía control.json.</summary>
public enum ComandoControl
{
    Apagar,
    Recargar,
    IniciarPares,
    PausarPares,
    DesbloquearBorrado,
    AutorizarPurgaEspejo
}
