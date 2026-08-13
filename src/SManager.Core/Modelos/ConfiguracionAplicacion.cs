using System.Text.Json.Serialization;

namespace SManager.Core.Modelos;

/// <summary>Esquema JSON de configuración (compatible con SManager v1).</summary>
public sealed class ConfiguracionAplicacion
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("intervalo_polling_segundos")]
    public int IntervaloPollingSegundos { get; set; } = 180;

    [JsonPropertyName("segundos_estabilidad_archivo")]
    public int SegundosEstabilidadArchivo { get; set; } = 3;

    [JsonPropertyName("num_copiadores_paralelos")]
    public int NumCopiadoresParalelos { get; set; } = 4;

    [JsonPropertyName("num_hidratadores_paralelos")]
    public int NumHidratadoresParalelos { get; set; } = 3;

    [JsonPropertyName("timeout_hidratacion_segundos")]
    public int TimeoutHidratacionSegundos { get; set; } = 300;

    [JsonPropertyName("intervalo_publicacion_estado_ms")]
    public int IntervaloPublicacionEstadoMs { get; set; } = 500;

    [JsonPropertyName("pares")]
    public List<ParSincronizacion> Pares { get; set; } = [];
}

public enum ModoSincronizacion
{
    /// <summary>Guarda todos los archivos de A en B. No borra nada en B ni en A. (Principal / Defecto)</summary>
    AcumulativoSinBorrado = 0,

    /// <summary>Copia de A a B y borra en A tras copia confirmada. Requiere Admin local.</summary>
    AcumulativoConBorradoOrigen = 1,

    /// <summary>Sincronización espejo idéntica A -> B. Replica copias y borrados de A en B.</summary>
    Espejo = 2
}

public sealed class ParSincronizacion
{
    [JsonPropertyName("id_par")]
    public string IdPar { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = "Nuevo par";

    [JsonPropertyName("habilitado")]
    public bool Habilitado { get; set; } = true;

    [JsonPropertyName("pausado")]
    public bool Pausado { get; set; }

    [JsonPropertyName("ruta_origen")]
    public string RutaOrigen { get; set; } = string.Empty;

    [JsonPropertyName("ruta_destino")]
    public string RutaDestino { get; set; } = string.Empty;

    [JsonPropertyName("filtro_inclusion")]
    public string FiltroInclusion { get; set; } = "*";

    [JsonPropertyName("filtro_exclusion")]
    public string FiltroExclusion { get; set; } = "~$*;*.tmp;*.partial;*.lnk";

    [JsonPropertyName("total_copiados")]
    public int TotalCopiados { get; set; }

    [JsonPropertyName("total_errores")]
    public int TotalErrores { get; set; }

    /// <summary>
    /// Segundos entre barridos de seguridad de este par.
    /// Null o &lt;= 0: usar <see cref="ConfiguracionAplicacion.IntervaloPollingSegundos"/> global.
    /// </summary>
    [JsonPropertyName("intervalo_polling_segundos")]
    public int? IntervaloPollingSegundos { get; set; }

    /// <summary>
    /// Modo de operación de este par de sincronización.
    /// </summary>
    [JsonPropertyName("modo_sincronizacion")]
    public ModoSincronizacion Modo { get; set; } = ModoSincronizacion.AcumulativoSinBorrado;

    /// <summary>
    /// Número máximo de archivos a purgar de golpe en destino en Modo Espejo antes de pausar por protección antidesastre.
    /// Default: 50.
    /// </summary>
    [JsonPropertyName("umbral_purga_masiva_espejo")]
    public int UmbralPurgaMasivaEspejo { get; set; } = 50;

    /// <summary>
    /// Obsoleto: Mantenido para compatibilidad con esquemas JSON anteriores.
    /// Mapea directamente hacia/desde <see cref="ModoSincronizacion.AcumulativoConBorradoOrigen"/>.
    /// </summary>
    [JsonPropertyName("borrar_en_origen")]
    public bool BorrarEnOrigen
    {
        get => Modo == ModoSincronizacion.AcumulativoConBorradoOrigen;
        set
        {
            if (value && Modo == ModoSincronizacion.AcumulativoSinBorrado)
            {
                Modo = ModoSincronizacion.AcumulativoConBorradoOrigen;
            }
        }
    }
}
