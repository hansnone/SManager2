using SManager.Core.Copia;
using SManager.Core.Modelos;
using SManager.Core.Motor;
using SManager.Core.Utilidades;
using SManager.Gui.Shared.Servicios;
using SManager.Ipc;

namespace SManager.Core.Tests;

/// <summary>
/// Pruebas unitarias de comparación incremental, preservación de timestamps,
/// filtros de subcarpetas, comando IPC AutorizarPurgaEspejo y vista previa detallada.
/// </summary>
public sealed class ComparadorSincronizacionTests : IDisposable
{
    private readonly string _directorioBase;
    private readonly string _origen;
    private readonly string _destino;

    public ComparadorSincronizacionTests()
    {
        _directorioBase = Path.Combine(Path.GetTempPath(), "SManager2Tests", Guid.NewGuid().ToString("N"));
        _origen = Path.Combine(_directorioBase, "origen");
        _destino = Path.Combine(_directorioBase, "destino");
        Directory.CreateDirectory(_origen);
        Directory.CreateDirectory(_destino);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directorioBase))
        {
            Directory.Delete(_directorioBase, recursive: true);
        }
    }

    [Fact]
    public void NecesitaCopia_DevuelveFalse_CuandoDestinoCoincide()
    {
        var par = CrearPar();
        var rutaOrigen = Path.Combine(_origen, "dato.txt");
        var rutaDestino = Path.Combine(_destino, "dato.txt");
        File.WriteAllText(rutaOrigen, "hola");
        File.WriteAllText(rutaDestino, "hola");
        File.SetLastWriteTimeUtc(rutaDestino, File.GetLastWriteTimeUtc(rutaOrigen));

        var info = new FileInfo(rutaOrigen);
        Assert.False(ComparadorSincronizacion.NecesitaCopia(info, par));
    }

    [Fact]
    public void NecesitaCopia_DevuelveTrue_CuandoFaltaEnDestino()
    {
        var par = CrearPar();
        var rutaOrigen = Path.Combine(_origen, "nuevo.txt");
        File.WriteAllText(rutaOrigen, "contenido");

        Assert.True(ComparadorSincronizacion.NecesitaCopia(new FileInfo(rutaOrigen), par));
    }

    [Fact]
    public void NecesitaCopia_DevuelveTrue_CuandoTamanoDifiereAunqueFechaSeaIgual()
    {
        var par = CrearPar();
        var rutaOrigen = Path.Combine(_origen, "modificado.txt");
        var rutaDestino = Path.Combine(_destino, "modificado.txt");
        var instante = new DateTime(2025, 5, 10, 12, 0, 0, DateTimeKind.Utc);

        File.WriteAllText(rutaOrigen, "contenido mucho mas largo");
        File.WriteAllText(rutaDestino, "corto");
        File.SetLastWriteTimeUtc(rutaOrigen, instante);
        File.SetLastWriteTimeUtc(rutaDestino, instante);

        Assert.True(ComparadorSincronizacion.NecesitaCopia(new FileInfo(rutaOrigen), par));
    }

    [Fact]
    public void ServicioCopia_PreservaLastWriteTimeUtc_YEvitaRecopiaEnSiguienteCiclo()
    {
        var par = CrearPar();
        var rutaOrigen = Path.Combine(_origen, "historico.txt");
        var rutaDestino = Path.Combine(_destino, "historico.txt");
        var fechaAntigua = new DateTime(2022, 3, 15, 8, 30, 0, DateTimeKind.Utc);

        File.WriteAllText(rutaOrigen, "documento con fecha original");
        File.SetLastWriteTimeUtc(rutaOrigen, fechaAntigua);

        var estado = new EstadoMotor
        {
            Config = new ConfiguracionAplicacion { Pares = [par] },
            Pares = [par],
            EnEjecucion = true
        };

        var servicioCopia = new ServicioCopia();
        var resultado = servicioCopia.EjecutarCopiaCondicional(estado, rutaOrigen, par);

        Assert.Equal(1, resultado.Copiados);
        Assert.Equal(fechaAntigua, File.GetLastWriteTimeUtc(rutaDestino));
        Assert.False(ComparadorSincronizacion.NecesitaCopia(new FileInfo(rutaOrigen), par));
    }

    [Fact]
    public void ServicioFiltros_ExcluyeSubcarpetasComoNodeModulesOGit()
    {
        var par = CrearPar();
        par.FiltroInclusion = "*";
        par.FiltroExclusion = "node_modules;.git;*.tmp";

        Assert.False(ServicioFiltros.PasaFiltros("index.js", par, @"web\node_modules\react\index.js"));
        Assert.False(ServicioFiltros.PasaFiltros("HEAD", par, @".git\HEAD"));
        Assert.True(ServicioFiltros.PasaFiltros("app.js", par, @"web\src\app.js"));
    }

    [Fact]
    public async Task ServicioIpc_SerializaYDeserializa_AutorizarPurgaEspejo()
    {
        var perfilPrueba = "TestIpc_" + Guid.NewGuid().ToString("N")[..8];
        var ipc = new ServicioIpc();
        try
        {
            await ipc.EnviarComandoAsync(perfilPrueba, ComandoControl.AutorizarPurgaEspejo);
            var leido = await ipc.LeerComandoPendienteAsync(perfilPrueba);
            Assert.Equal(ComandoControl.AutorizarPurgaEspejo, leido);
        }
        finally
        {
            var dir = Path.GetDirectoryName(RutasDatos.ResolverRutaEstado(perfilPrueba));
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void ServicioAnalisisCambios_GeneraDetalleDeArchivosNuevosModificadosYHuerfanosEspejo()
    {
        var par = CrearPar();
        par.Nombre = "Backup Proyecto";
        par.Modo = ModoSincronizacion.Espejo;

        // 1 archivo nuevo en origen
        File.WriteAllText(Path.Combine(_origen, "nuevo.cs"), "class Nuevo {}");

        // 1 archivo modificado (distinto tamaño)
        File.WriteAllText(Path.Combine(_origen, "editado.cs"), "class EditadoMasLargo {}");
        File.WriteAllText(Path.Combine(_destino, "editado.cs"), "corto");

        // 1 archivo huérfano solo en destino (se eliminará en modo espejo)
        File.WriteAllText(Path.Combine(_destino, "antiguo.cs"), "obsoleto");

        var analisis = ServicioAnalisisCambios.AnalizarPares([par]);

        Assert.Single(analisis.PorPar);
        var resPar = analisis.PorPar[0];
        Assert.Equal(1, resPar.ArchivosNuevos);
        Assert.Equal(1, resPar.ArchivosModificados);
        Assert.Equal(1, resPar.ArchivosHuerfanosEspejo);
        Assert.Equal(3, resPar.ArchivosPrevistos.Count);
        Assert.Contains(resPar.ArchivosPrevistos, a => a.RutaRelativa == "nuevo.cs" && a.TipoAccion == "Nuevo");
        Assert.Contains(resPar.ArchivosPrevistos, a => a.RutaRelativa == "editado.cs" && a.TipoAccion == "Modificado");
        Assert.Contains(resPar.ArchivosPrevistos, a => a.RutaRelativa == "antiguo.cs" && a.TipoAccion == "Eliminar (espejo)");
    }

    private ParSincronizacion CrearPar() => new()
    {
        IdPar = "test",
        RutaOrigen = _origen,
        RutaDestino = _destino
    };
}
