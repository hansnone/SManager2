using SManager.Core.Copia;
using SManager.Core.Modelos;
using SManager.Core.Motor;

namespace SManager.Core.Tests;

public sealed class ServicioCopiaBorradoOrigenTests : IDisposable
{
    private readonly string _directorioBase;
    private readonly string _origen;
    private readonly string _destino;
    private readonly ServicioCopia _servicioCopia = new();

    public ServicioCopiaBorradoOrigenTests()
    {
        _directorioBase = Path.Combine(Path.GetTempPath(), "SManagerBorradoTests", Guid.NewGuid().ToString("N"));
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
    public void Copia_NoBorraOrigen_CuandoBorrarEnOrigenEsFalse()
    {
        var par = CrearPar(borrarEnOrigen: false);
        var archivoOrigen = Path.Combine(_origen, "documento.txt");
        File.WriteAllText(archivoOrigen, "Contenido de prueba");

        var estado = CrearEstado(desbloqueadoAdmin: true, par);
        var resultado = _servicioCopia.EjecutarCopiaCondicional(estado, archivoOrigen, par);

        Assert.Equal(1, resultado.Copiados);
        Assert.True(File.Exists(archivoOrigen), "El origen debe mantenerse si BorrarEnOrigen es false");
        Assert.True(File.Exists(Path.Combine(_destino, "documento.txt")), "El destino debe existir");
    }

    [Fact]
    public void Copia_NoBorraOrigen_CuandoSesionEstaBloqueadaPorAdmin()
    {
        var par = CrearPar(borrarEnOrigen: true);
        var archivoOrigen = Path.Combine(_origen, "documento.txt");
        File.WriteAllText(archivoOrigen, "Contenido de prueba");

        var estado = CrearEstado(desbloqueadoAdmin: false, par); // Bloqueado
        var resultado = _servicioCopia.EjecutarCopiaCondicional(estado, archivoOrigen, par);

        Assert.Equal(1, resultado.Copiados);
        Assert.True(File.Exists(archivoOrigen), "El origen debe mantenerse si la sesión no fue desbloqueada por un admin");
        Assert.True(File.Exists(Path.Combine(_destino, "documento.txt")), "El destino debe existir");
    }

    [Fact]
    public void Copia_BorraOrigen_CuandoSesionEstaDesbloqueadaYBorrarEnOrigenTrue()
    {
        var par = CrearPar(borrarEnOrigen: true);
        var archivoOrigen = Path.Combine(_origen, "documento.txt");
        File.WriteAllText(archivoOrigen, "Contenido de prueba");

        var estado = CrearEstado(desbloqueadoAdmin: true, par); // Desbloqueado por Admin
        var resultado = _servicioCopia.EjecutarCopiaCondicional(estado, archivoOrigen, par);

        Assert.Equal(1, resultado.Copiados);
        Assert.False(File.Exists(archivoOrigen), "El origen debe borrarse al estar desbloqueado y configurado");
        Assert.True(File.Exists(Path.Combine(_destino, "documento.txt")), "El destino debe existir");
    }

    private ParSincronizacion CrearPar(bool borrarEnOrigen) => new()
    {
        IdPar = "par-test-borrado",
        Nombre = "Test Borrado",
        Habilitado = true,
        RutaOrigen = _origen,
        RutaDestino = _destino,
        BorrarEnOrigen = borrarEnOrigen
    };

    private static EstadoMotor CrearEstado(bool desbloqueadoAdmin, ParSincronizacion par)
    {
        var config = new ConfiguracionAplicacion
        {
            Pares = [par]
        };

        return new EstadoMotor
        {
            Config = config,
            Pares = [par],
            EnEjecucion = true,
            SesionBorradoDesbloqueada = desbloqueadoAdmin
        };
    }
}
