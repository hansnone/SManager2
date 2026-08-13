using SManager.Core.Utilidades;

namespace SManager.Core.Tests;

public sealed class ServicioAutenticacionAdminTests
{
    [Fact]
    public void ValidarCredenciales_DevuelveFalse_ConUsuarioVacio()
    {
        var resultado = ServicioAutenticacionAdmin.ValidarCredencialesAdministradorLocal("", "pass", ".", out var error);
        Assert.False(resultado);
        Assert.NotNull(error);
        Assert.Contains("vacío", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarCredenciales_DevuelveFalse_ConCredencialesFalsas()
    {
        var resultado = ServicioAutenticacionAdmin.ValidarCredencialesAdministradorLocal("UsuarioFalsoInexistente999", "BadPass123!", ".", out var error);
        Assert.False(resultado);
        Assert.NotNull(error);
    }

    [Fact]
    public void ProcesoActualEsAdministrador_NoLanzaExcepcion()
    {
        var esAdmin = ServicioAutenticacionAdmin.ProcesoActualEsAdministrador();
        // Simplemente verifica que el método se ejecute sin excepciones en el SO actual
        Assert.True(esAdmin || !esAdmin);
    }
}
