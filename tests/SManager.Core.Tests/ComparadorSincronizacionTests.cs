using SManager.Core.Copia;
using SManager.Core.Modelos;

namespace SManager.Core.Tests;

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

    private ParSincronizacion CrearPar() => new()
    {
        IdPar = "test",
        RutaOrigen = _origen,
        RutaDestino = _destino
    };
}
