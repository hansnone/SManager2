using System.Text.Json.Serialization;

namespace SManager.Ipc.Modelos;

/// <summary>Telemetría publicada por el demonio (compatible con esquema v1).</summary>
public sealed class EstadoPerfil
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("perfil")]
    public string Perfil { get; set; } = string.Empty;

    [JsonPropertyName("pid")]
    public int Pid { get; set; }

    [JsonPropertyName("en_ejecucion")]
    public bool EnEjecucion { get; set; }

    [JsonPropertyName("aceptar_nuevos_trabajos")]
    public bool AceptarNuevosTrabajos { get; set; }

    [JsonPropertyName("proximo_polling_en_segundos")]
    public int? ProximoPollingEnSegundos { get; set; }

    [JsonPropertyName("cola_copia_pendiente")]
    public int ColaCopiaPendiente { get; set; }

    [JsonPropertyName("archivos_unicos_pendientes")]
    public int ArchivosUnicosPendientes { get; set; }

    [JsonPropertyName("duplicados_evitados")]
    public int DuplicadosEvitados { get; set; }

    [JsonPropertyName("hidrataciones_activas")]
    public int HidratacionesActivas { get; set; }

    [JsonPropertyName("totales")]
    public TotalesEstado Totales { get; set; } = new();

    [JsonPropertyName("pares")]
    public List<ResumenPar> Pares { get; set; } = [];

    [JsonPropertyName("actividad_reciente")]
    public List<EntradaActividad> ActividadReciente { get; set; } = [];

    [JsonPropertyName("copias_en_curso")]
    public List<CopiaEnCurso> CopiasEnCurso { get; set; } = [];

    [JsonPropertyName("actualizado_utc")]
    public string ActualizadoUtc { get; set; } = string.Empty;

    [JsonPropertyName("inicio_sesion_utc")]
    public string? InicioSesionUtc { get; set; }

    [JsonPropertyName("recursos")]
    public RecursosProceso? Recursos { get; set; }
}

public sealed class TotalesEstado
{
    [JsonPropertyName("copiados")]
    public int Copiados { get; set; }

    [JsonPropertyName("errores")]
    public int Errores { get; set; }

    [JsonPropertyName("bytes_escritos")]
    public long BytesEscritos { get; set; }
}

public sealed class ResumenPar
{
    [JsonPropertyName("id_par")]
    public string IdPar { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("estado")]
    public string Estado { get; set; } = "OK";

    [JsonPropertyName("copiados")]
    public int Copiados { get; set; }

    [JsonPropertyName("errores")]
    public int Errores { get; set; }

    [JsonPropertyName("ultima_sincronizacion")]
    public string? UltimaSincronizacion { get; set; }

    [JsonPropertyName("proximo_polling_en_segundos")]
    public int? ProximoPollingEnSegundos { get; set; }
}

public sealed class EntradaActividad
{
    [JsonPropertyName("hora")]
    public string Hora { get; set; } = string.Empty;

    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [JsonPropertyName("archivo")]
    public string Archivo { get; set; } = string.Empty;

    [JsonPropertyName("id_par")]
    public string IdPar { get; set; } = string.Empty;
}

public sealed class CopiaEnCurso
{
    [JsonPropertyName("archivo")]
    public string Archivo { get; set; } = string.Empty;

    [JsonPropertyName("id_par")]
    public string IdPar { get; set; } = string.Empty;

    [JsonPropertyName("copiador")]
    public int Copiador { get; set; }

    [JsonPropertyName("bytes_totales")]
    public long BytesTotales { get; set; }

    [JsonPropertyName("bytes_copiados")]
    public long BytesCopiados { get; set; }

    [JsonPropertyName("porcentaje")]
    public int Porcentaje { get; set; }

    [JsonPropertyName("eta_segundos")]
    public int? EtaSegundos { get; set; }
}

/// <summary>Consumo de RAM y CPU del proceso demonio en el instante de publicación.</summary>
public sealed class RecursosProceso
{
    [JsonPropertyName("memoria_trabajo_bytes")]
    public long MemoriaTrabajoBytes { get; set; }

    [JsonPropertyName("cpu_porcentaje")]
    public double CpuPorcentaje { get; set; }
}
