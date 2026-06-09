using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SManager.Gui.WinUI.Models;
using SManager.Gui.WinUI.Servicios;
using SManager.Ipc;
using SManager.Ipc.Modelos;

namespace SManager.Gui.WinUI.ViewModels;
/// <summary>Métricas agregadas mostradas en la sección Estadísticas.</summary>
public partial class EstadisticasPanelViewModel : ObservableObject
{
    private readonly MuestreadorRecursosPorPid _muestreadorRecursosPorPid = new();

    private long _ultimosBytesEscritos;
    private DateTime _ultimaMuestraVelocidadUtc;

    public ObservableCollection<EstadisticaParItemViewModel> Pares { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarAvisoSinDatos))]
    private bool _hayDatos;

    /// <summary>Para InfoBar.IsOpen (bool), no usar el convertidor de Visibility.</summary>
    public bool MostrarAvisoSinDatos => !HayDatos;

    [ObservableProperty]
    private string _textoAvisoSinDatos =
        "Inicia el demonio para ver estadísticas en tiempo real. Si acaba de parar, puede quedar la última instantánea en disco.";

    [ObservableProperty]
    private string _textoTiempoSesion = "—";

    [ObservableProperty]
    private string _textoInicioSesion = "—";

    [ObservableProperty]
    private string _textoPid = "—";

    [ObservableProperty]
    private string _textoArchivosCopiados = "0";

    [ObservableProperty]
    private string _textoErrores = "0";

    [ObservableProperty]
    private string _textoBytesEscritos = "0 B";

    [ObservableProperty]
    private string _textoVelocidadEscritura = "—";

    [ObservableProperty]
    private string _textoDuplicadosEvitados = "0";

    [ObservableProperty]
    private string _textoColaPendiente = "0";

    [ObservableProperty]
    private string _textoHidrataciones = "0";

    [ObservableProperty]
    private string _textoCopiasActivas = "0";

    [ObservableProperty]
    private string _textoMemoriaDemonio = "—";

    [ObservableProperty]
    private string _textoCpuDemonio = "—";

    [ObservableProperty]
    private string _textoTamanoRegistro = "—";

    [ObservableProperty]
    private string _textoUltimaActualizacion = "—";

    /// <summary>Actualiza métricas desde telemetría IPC y datos locales del log.</summary>
    public void ActualizarDesdeEstado(EstadoPerfil? estado, long tamanoLogBytes, string nombrePerfil)
    {
        TextoTamanoRegistro = ServicioFormateoEstadisticas.FormatearBytes(tamanoLogBytes);

        if (estado is null)
        {
            // Sin demonio en vivo: vaciar métricas pero conservar el tamaño del log en disco.
            ReiniciarPresentacion();
            TextoTamanoRegistro = ServicioFormateoEstadisticas.FormatearBytes(tamanoLogBytes);
            return;
        }

        HayDatos = true;
        TextoArchivosCopiados = estado.Totales.Copiados.ToString();
        TextoErrores = estado.Totales.Errores.ToString();
        TextoBytesEscritos = ServicioFormateoEstadisticas.FormatearBytes(estado.Totales.BytesEscritos);
        TextoDuplicadosEvitados = estado.DuplicadosEvitados.ToString();
        TextoColaPendiente = estado.ColaCopiaPendiente.ToString();
        TextoHidrataciones = estado.HidratacionesActivas.ToString();
        TextoCopiasActivas = estado.CopiasEnCurso.Count.ToString();

        var pidEnVivo = ResolverPidEnVivo(nombrePerfil, estado.Pid);
        var demonioActivo = estado.EnEjecucion || pidEnVivo > 0;
        TextoPid = pidEnVivo > 0 ? pidEnVivo.ToString() : "—";
        TextoUltimaActualizacion =
            ServicioFormateoEstadisticas.FormatearInstanteUtc(estado.ActualizadoUtc);

        var inicioSesion = ResolverInicioSesion(estado, pidEnVivo);
        TextoInicioSesion = inicioSesion.HasValue
            ? ServicioFormateoEstadisticas.FormatearInstanteUtc(inicioSesion.Value.ToString("o"))
            : ServicioFormateoEstadisticas.FormatearInstanteUtc(estado.InicioSesionUtc);

        if (inicioSesion.HasValue && demonioActivo)
        {
            TextoTiempoSesion = ServicioFormateoEstadisticas.FormatearDuracion(
                DateTimeOffset.UtcNow - inicioSesion.Value);
        }
        else if (inicioSesion.HasValue
                 && DateTimeOffset.TryParse(estado.ActualizadoUtc, out var fin))
        {
            TextoTiempoSesion = ServicioFormateoEstadisticas.FormatearDuracion(fin - inicioSesion.Value);
        }
        else
        {
            TextoTiempoSesion = "—";
        }

        AplicarRecursosDemonio(estado, pidEnVivo);

        ActualizarVelocidadEscritura(estado.Totales.BytesEscritos);
        ActualizarListaPares(estado);
    }

    private void ActualizarVelocidadEscritura(long bytesEscritos)
    {
        var ahora = DateTime.UtcNow;
        if (_ultimaMuestraVelocidadUtc != default)
        {
            var segundos = (ahora - _ultimaMuestraVelocidadUtc).TotalSeconds;
            if (segundos >= 0.4)
            {
                var delta = bytesEscritos - _ultimosBytesEscritos;
                if (delta >= 0)
                {
                    TextoVelocidadEscritura = ServicioFormateoEstadisticas.FormatearVelocidad(
                        delta / segundos);
                }

                _ultimosBytesEscritos = bytesEscritos;
                _ultimaMuestraVelocidadUtc = ahora;
                return;
            }
        }

        _ultimosBytesEscritos = bytesEscritos;
        _ultimaMuestraVelocidadUtc = ahora;
    }

    private void ActualizarListaPares(EstadoPerfil estado)
    {
        var origen = estado.Pares
            .Select(p => EstadisticaParItemViewModel.DesdeResumen(
                p.Nombre,
                p.Estado,
                p.Copiados,
                p.Errores,
                p.UltimaSincronizacion))
            .ToList();

        ServicioSincronizacionLista.SincronizarHistorial(
            Pares,
            origen,
            static (a, b) =>
                a.Nombre == b.Nombre
                && a.Estado == b.Estado
                && a.Copiados == b.Copiados
                && a.Errores == b.Errores
                && a.UltimaSincronizacion == b.UltimaSincronizacion);
    }

    /// <summary>Reinicia contadores de velocidad al cambiar de perfil.</summary>
    public void ReiniciarMuestreo()
    {
        _ultimosBytesEscritos = 0;
        _ultimaMuestraVelocidadUtc = default;
        _muestreadorRecursosPorPid.Reiniciar();
        TextoVelocidadEscritura = "—";
        Pares.Clear();
        HayDatos = false;
    }

    /// <summary>Deja el panel como recién abierto tras limpiar datos del perfil.</summary>
    public void ReiniciarPresentacion()
    {
        ReiniciarMuestreo();
        TextoTiempoSesion = "—";
        TextoInicioSesion = "—";
        TextoPid = "—";
        TextoArchivosCopiados = "0";
        TextoErrores = "0";
        TextoBytesEscritos = "0 B";
        TextoDuplicadosEvitados = "0";
        TextoColaPendiente = "0";
        TextoHidrataciones = "0";
        TextoCopiasActivas = "0";
        TextoMemoriaDemonio = "—";
        TextoCpuDemonio = "—";
        TextoTamanoRegistro = "0 B";
        TextoUltimaActualizacion = "—";
    }

    /// <summary>IPC nuevo trae inicio_sesion_utc; si falta, inferimos desde el PID en vivo.</summary>
    private DateTimeOffset? ResolverInicioSesion(EstadoPerfil estado, int pidEnVivo)
    {
        if (DateTimeOffset.TryParse(estado.InicioSesionUtc, out var inicioIpc))
        {
            return inicioIpc;
        }

        return _muestreadorRecursosPorPid.ObtenerInicioUtc(pidEnVivo);
    }

    /// <summary>Preferimos el PID del fichero smanager.pid frente al JSON en caché.</summary>
    private static int ResolverPidEnVivo(string nombrePerfil, int pidEstado)
    {
        var rutaPid = RutasDatos.ResolverRutaPid(nombrePerfil);
        if (File.Exists(rutaPid) && int.TryParse(File.ReadAllText(rutaPid).Trim(), out var pid))
        {
            return pid;
        }

        return pidEstado;
    }

    /// <summary>Telemetría IPC preferida; si el demonio es antiguo, muestreamos el proceso por PID.</summary>
    private void AplicarRecursosDemonio(EstadoPerfil estado, int pidEnVivo)
    {
        if (estado.Recursos is not null)
        {
            TextoMemoriaDemonio = ServicioFormateoEstadisticas.FormatearBytes(
                estado.Recursos.MemoriaTrabajoBytes);
            TextoCpuDemonio = ServicioFormateoEstadisticas.FormatearPorcentajeCpu(
                estado.Recursos.CpuPorcentaje);
            return;
        }

        var muestra = _muestreadorRecursosPorPid.Muestrear(pidEnVivo);
        if (muestra is null)
        {
            TextoMemoriaDemonio = "—";
            TextoCpuDemonio = "—";
            return;
        }

        TextoMemoriaDemonio = ServicioFormateoEstadisticas.FormatearBytes(muestra.Value.MemoriaTrabajoBytes);
        TextoCpuDemonio = ServicioFormateoEstadisticas.FormatearPorcentajeCpu(muestra.Value.CpuPorcentaje);
    }
}
