using SManager.Core.Modelos;
using SManager.Core.Motor;

namespace SManager.Core.Tests;

public sealed class PoliticaPollingTests
{
    [Fact]
    public void ResolverIntervalo_UsaGlobal_CuandoParNoTieneValor()
    {
        var config = new ConfiguracionAplicacion { IntervaloPollingSegundos = 600 };
        var par = new ParSincronizacion { IntervaloPollingSegundos = null };

        Assert.Equal(600, PoliticaPolling.ResolverIntervaloSegundos(par, config));
    }

    [Fact]
    public void ResolverIntervalo_UsaPar_CuandoTieneValorPropio()
    {
        var config = new ConfiguracionAplicacion { IntervaloPollingSegundos = 600 };
        var par = new ParSincronizacion { IntervaloPollingSegundos = 120 };

        Assert.Equal(120, PoliticaPolling.ResolverIntervaloSegundos(par, config));
    }

    [Fact]
    public void SolicitarEscaneoPorPolling_Encola_SiEscaneoEnCurso()
    {
        var estado = CrearEstadoMinimo();
        estado.EscaneoEnCursoPorPar["par-a"] = true;

        estado.SolicitarEscaneoPorPolling("par-a");

        Assert.False(estado.PeticionEscaneoCompleto.GetValueOrDefault("par-a"));
        Assert.True(estado.EscaneoPollingPendientePorPar["par-a"]);
    }

    [Fact]
    public void SolicitarEscaneoPorPolling_Dispara_Inmediato_SiLibre()
    {
        var estado = CrearEstadoMinimo();

        estado.SolicitarEscaneoPorPolling("par-a");

        Assert.True(estado.PeticionEscaneoCompleto["par-a"]);
        Assert.True(estado.PeticionEscaneoPorPolling["par-a"]);
        Assert.False(estado.EscaneoPollingPendientePorPar.GetValueOrDefault("par-a"));
    }

    private static EstadoMotor CrearEstadoMinimo() =>
        new()
        {
            Config = new ConfiguracionAplicacion(),
            Pares = []
        };
}
