using SManager.Host;
using SManager.Host.Servicios;

var opciones = AnalizadorArgumentos.Analizar(args);

var builder = Host.CreateApplicationBuilder(args);

// El demonio por perfil no es servicio SCM: evitar EventLog (falta en despliegue junto a la GUI).
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

if (opciones.ModoDemonio)
{
    builder.Services.AddSingleton(opciones);
    builder.Services.AddHostedService<DemonioPerfilWorker>();
}
else
{
    builder.Services.AddWindowsService(o => o.ServiceName = "SManager2");
    builder.Services.AddHostedService<SupervisorServicioWindows>();
}

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
