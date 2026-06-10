using SManager.Core.Copia;
using SManager.Core.Modelos;

namespace SManager.Core.Tests;

public sealed class IndiceMetadatosDestinoTests : IDisposable
{
    private readonly string _directorioBase;
    private readonly string _origen;
    private readonly string _destino;

    public IndiceMetadatosDestinoTests()
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
    public void Construir_IndexaArchivosDelDestino()
    {
        Directory.CreateDirectory(Path.Combine(_destino, "sub"));
        File.WriteAllText(Path.Combine(_destino, "a.txt"), "uno");
        File.WriteAllText(Path.Combine(_destino, "sub", "b.txt"), "dos");

        var par = CrearPar();
        var indice = IndiceMetadatosDestino.Construir(par, CancellationToken.None);

        Assert.Equal(2, indice.CantidadArchivos);
        Assert.True(indice.TryObtener("a.txt", out _));
        Assert.True(indice.TryObtener("sub/b.txt", out _) || indice.TryObtener(@"sub\b.txt", out _));
    }

    [Fact]
    public void NecesitaCopia_ConIndice_CoincideConComparacionDirecta()
    {
        var par = CrearPar();
        var rutaOrigen = Path.Combine(_origen, "cache.bgeo.sc");
        var rutaDestino = Path.Combine(_destino, "cache.bgeo.sc");
        File.WriteAllText(rutaOrigen, "datos");
        File.WriteAllText(rutaDestino, "datos");
        File.SetLastWriteTimeUtc(rutaDestino, File.GetLastWriteTimeUtc(rutaOrigen));

        var info = new FileInfo(rutaOrigen);
        var indice = IndiceMetadatosDestino.Construir(par, CancellationToken.None);

        Assert.False(ComparadorSincronizacion.NecesitaCopia(info, par, indice));
        Assert.False(ComparadorSincronizacion.NecesitaCopia(info, par));
    }

    private ParSincronizacion CrearPar() => new()
    {
        IdPar = "test",
        RutaOrigen = _origen,
        RutaDestino = _destino
    };
}
