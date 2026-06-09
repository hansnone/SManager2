namespace SManager.Gui.WinUI.Models;

/// <summary>Respuesta del usuario ante cambios de configuración aún no guardados en disco.</summary>
public enum DecisionCambiosPendientes
{
    /// <summary>Continuar sin escribir el JSON (descartar cambios en memoria).</summary>
    ContinuarSinGuardar,

    /// <summary>Guardar en disco y después continuar la acción solicitada.</summary>
    GuardarYContinuar,

    /// <summary>Abortar la acción (permanecer en el perfil o ventana actual).</summary>
    Cancelar
}
