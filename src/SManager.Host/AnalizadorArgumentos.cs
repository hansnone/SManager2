namespace SManager.Host;

/// <summary>Parsea argumentos de línea de comandos del host.</summary>
public static class AnalizadorArgumentos
{
    public static OpcionesArranque Analizar(string[] args)
    {
        var opciones = new OpcionesArranque();
        string? perfil = null;
        string? config = null;
        var modoServicio = false;
        var modoDemonio = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "--servicio":
                case "-servicio":
                    modoServicio = true;
                    break;
                case "--demonio":
                case "-demonio":
                    modoDemonio = true;
                    break;
                case "--perfil":
                case "-perfil":
                    if (i + 1 < args.Length)
                    {
                        perfil = args[++i];
                    }
                    break;
                case "--config":
                case "-config":
                case "--configpath":
                case "-configpath":
                    if (i + 1 < args.Length)
                    {
                        config = args[++i];
                    }
                    break;
            }
        }

        return new OpcionesArranque
        {
            ModoServicio = modoServicio,
            ModoDemonio = modoDemonio,
            Perfil = perfil ?? "General",
            RutaConfiguracion = config
        };
    }
}
