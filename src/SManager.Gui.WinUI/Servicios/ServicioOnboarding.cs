namespace SManager.Gui.WinUI.Servicios;

/// <summary>Reglas y persistencia del flujo de primer uso.</summary>
public static class ServicioOnboarding
{
    public const string ConsejoIniciar = "iniciar";
    public const string ConsejoAnalizar = "analizar";
    public const string ConsejoSincronizacion = "sincronizacion";
    public const string ConsejoOrigenDestino = "origen_destino";

    /// <summary>True si conviene mostrar el asistente al abrir la app.</summary>
    public static bool DebeMostrarAsistenteAutomatico(bool tienePares)
    {
        var preferencias = ServicioPreferenciasGui.Cargar();
        return !preferencias.AsistenteCompletado && !tienePares;
    }

    public static void MarcarAsistenteCompletado()
    {
        var preferencias = ServicioPreferenciasGui.Cargar();
        preferencias.AsistenteCompletado = true;
        ServicioPreferenciasGui.Guardar(preferencias);
    }

    public static bool DebeMostrarConsejo(string idConsejo)
    {
        var preferencias = ServicioPreferenciasGui.Cargar();
        if (!preferencias.MostrarConsejosContextuales)
        {
            return false;
        }

        return !preferencias.ConsejosVistos.Contains(idConsejo, StringComparer.OrdinalIgnoreCase);
    }

    public static void MarcarConsejoVisto(string idConsejo)
    {
        var preferencias = ServicioPreferenciasGui.Cargar();
        if (!preferencias.ConsejosVistos.Contains(idConsejo, StringComparer.OrdinalIgnoreCase))
        {
            preferencias.ConsejosVistos.Add(idConsejo);
            ServicioPreferenciasGui.Guardar(preferencias);
        }
    }

    public static void RestablecerConsejos()
    {
        var preferencias = ServicioPreferenciasGui.Cargar();
        preferencias.ConsejosVistos.Clear();
        ServicioPreferenciasGui.Guardar(preferencias);
    }
}
