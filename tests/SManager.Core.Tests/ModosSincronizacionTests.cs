using SManager.Core.Copia;
using SManager.Core.Modelos;
using SManager.Core.Motor;
using SManager.Core.Vigia;

namespace SManager.Core.Tests;

public sealed class ModosSincronizacionTests : IDisposable
{
    private readonly string _directorioBase;
    private readonly string _origen;
    private readonly string _destino;
    private readonly ServicioCopia _servicioCopia = new();

    public ModosSincronizacionTests()
    {
        _directorioBase = Path.Combine(Path.GetTempPath(), "SManagerModosTests", Guid.NewGuid().ToString("N"));
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
    public void Modo0_AcumulativoSinBorrado_ConservaOrigenYDestino()
    {
        var par = new ParSincronizacion
        {
            IdPar = "p0",
            RutaOrigen = _origen,
            RutaDestino = _destino,
            Modo = ModoSincronizacion.AcumulativoSinBorrado
        };

        var archivo = Path.Combine(_origen, "test1.txt");
        File.WriteAllText(archivo, "Contenido");

        var estado = CrearEstado(desbloqueadoAdmin: true, par);
        var resultado = _servicioCopia.EjecutarCopiaCondicional(estado, archivo, par);

        Assert.Equal(1, resultado.Copiados);
        Assert.True(File.Exists(archivo), "El origen debe conservarse en modo acumulativo sin borrado");
        Assert.True(File.Exists(Path.Combine(_destino, "test1.txt")), "El destino debe existir");
    }

    [Fact]
    public void Modo1_AcumulativoConBorradoOrigen_EliminaOrigenSiAdminDesbloqueado()
    {
        var par = new ParSincronizacion
        {
            IdPar = "p1",
            RutaOrigen = _origen,
            RutaDestino = _destino,
            Modo = ModoSincronizacion.AcumulativoConBorradoOrigen
        };

        var archivo = Path.Combine(_origen, "test1.txt");
        File.WriteAllText(archivo, "Contenido");

        var estado = CrearEstado(desbloqueadoAdmin: true, par);
        var resultado = _servicioCopia.EjecutarCopiaCondicional(estado, archivo, par);

        Assert.Equal(1, resultado.Copiados);
        Assert.False(File.Exists(archivo), "El origen debe borrarse tras la copia si admin está desbloqueado");
        Assert.True(File.Exists(Path.Combine(_destino, "test1.txt")), "El destino debe existir");
    }

    [Fact]
    public void Modo1_AcumulativoConBorradoOrigen_ConservaOrigenSiBloqueado()
    {
        var par = new ParSincronizacion
        {
            IdPar = "p1_lock",
            RutaOrigen = _origen,
            RutaDestino = _destino,
            Modo = ModoSincronizacion.AcumulativoConBorradoOrigen
        };

        var archivo = Path.Combine(_origen, "test1.txt");
        File.WriteAllText(archivo, "Contenido");

        var estado = CrearEstado(desbloqueadoAdmin: false, par); // Sesión bloqueada
        var resultado = _servicioCopia.EjecutarCopiaCondicional(estado, archivo, par);

        Assert.Equal(1, resultado.Copiados);
        Assert.True(File.Exists(archivo), "El origen NO debe borrarse si la sesión está bloqueada por admin");
    }

    [Fact]
    public async Task Modo2_Espejo_PurgaArchivosHuerfanosEnDestino()
    {
        var par = new ParSincronizacion
        {
            IdPar = "p2_espejo",
            RutaOrigen = _origen,
            RutaDestino = _destino,
            Modo = ModoSincronizacion.Espejo
        };

        // Archivo normal en origen y destino (previamente sincronizado)
        var validoOrigen = Path.Combine(_origen, "valido.txt");
        var validoDestino = Path.Combine(_destino, "valido.txt");
        File.WriteAllText(validoOrigen, "valido");
        File.WriteAllText(validoDestino, "valido");

        // Archivo huérfano en destino que ya no existe en origen
        File.WriteAllText(Path.Combine(_destino, "huerfano.txt"), "borrado en origen");

        var estado = CrearEstado(desbloqueadoAdmin: false, par);

        await using (var vigia = new VigiaPar(estado, par.IdPar))
        {
            vigia.Iniciar();
            await Task.Delay(400);
        }

        Assert.True(File.Exists(Path.Combine(_destino, "valido.txt")), "El archivo válido debe sincronizarse o mantenerse");
        Assert.False(File.Exists(Path.Combine(_destino, "huerfano.txt")), "El archivo huérfano en destino debe purgarse en Modo Espejo");
    }

    [Fact]
    public async Task Modo2_Espejo_GuardianAntiPurgaMasiva_DetienePurgaSiSuperaUmbral()
    {
        var par = new ParSincronizacion
        {
            IdPar = "p2_guardian",
            RutaOrigen = _origen,
            RutaDestino = _destino,
            Modo = ModoSincronizacion.Espejo,
            UmbralPurgaMasivaEspejo = 3
        };

        // Crear 5 archivos huérfanos en destino (supera el umbral de 3)
        for (int i = 1; i <= 5; i++)
        {
            File.WriteAllText(Path.Combine(_destino, $"huerfano_{i}.txt"), "purga masiva");
        }

        var estado = CrearEstado(desbloqueadoAdmin: false, par);

        await using (var vigia = new VigiaPar(estado, par.IdPar))
        {
            vigia.Iniciar();
            await Task.Delay(400);
        }

        // Ningún archivo debe ser eliminado porque se activó el Guardián Antidesastre
        for (int i = 1; i <= 5; i++)
        {
            Assert.True(File.Exists(Path.Combine(_destino, $"huerfano_{i}.txt")), $"El archivo huérfano {i} debió ser protegido por el guardián");
        }
    }

    [Fact]
    public async Task Modo2_Espejo_PurgaMasivaAutorizada_EjecutaSoloUnaVez()
    {
        var par = new ParSincronizacion
        {
            IdPar = "p2_autorizado",
            RutaOrigen = _origen,
            RutaDestino = _destino,
            Modo = ModoSincronizacion.Espejo,
            UmbralPurgaMasivaEspejo = 3
        };

        // Crear 5 archivos huérfanos en destino
        for (int i = 1; i <= 5; i++)
        {
            File.WriteAllText(Path.Combine(_destino, $"huerfano_{i}.txt"), "purga masiva");
        }

        var estado = CrearEstado(desbloqueadoAdmin: false, par);
        // Otorgar autorización intencionada de única pasada
        estado.AutorizacionPurgaMasivaUnaVez[par.IdPar] = true;

        await using (var vigia = new VigiaPar(estado, par.IdPar))
        {
            vigia.Iniciar();
            await Task.Delay(400);
        }

        // Todos los archivos huérfanos debieron eliminarse tras la autorización explícita
        for (int i = 1; i <= 5; i++)
        {
            Assert.False(File.Exists(Path.Combine(_destino, $"huerfano_{i}.txt")), $"El archivo huérfano {i} debió eliminarse al estar autorizado");
        }

        // La autorización debió ser consumida
        Assert.False(estado.AutorizacionPurgaMasivaUnaVez.ContainsKey(par.IdPar), "La autorización debe consumirse tras una única pasada");
    }

    private static EstadoMotor CrearEstado(bool desbloqueadoAdmin, ParSincronizacion par)
    {
        var config = new ConfiguracionAplicacion { Pares = [par] };
        return new EstadoMotor
        {
            Config = config,
            Pares = [par],
            EnEjecucion = true,
            SesionBorradoDesbloqueada = desbloqueadoAdmin
        };
    }
}
