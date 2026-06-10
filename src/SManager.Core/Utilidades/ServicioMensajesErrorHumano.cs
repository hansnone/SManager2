namespace SManager.Core.Utilidades;

/// <summary>Traduce excepciones y mensajes técnicos a texto comprensible para el usuario.</summary>
public static class ServicioMensajesErrorHumano
{
    /// <summary>Resume una excepción sin jerga de .NET cuando sea posible.</summary>
    public static string TraducirExcepcion(Exception ex)
    {
        var actual = ex;
        while (actual.InnerException is not null)
        {
            actual = actual.InnerException;
        }

        return actual switch
        {
            UnauthorizedAccessException =>
                "SManager no tiene permisos para acceder a un archivo o carpeta del sistema.",
            DirectoryNotFoundException =>
                "No se encontró una carpeta necesaria (puede que la unidad no esté conectada).",
            FileNotFoundException =>
                "No se encontró un archivo necesario para continuar.",
            IOException io when Contiene(io.Message, "being used", "en uso", "locked", "bloqueado") =>
                "Un archivo está en uso por otra aplicación.",
            IOException io when Contiene(io.Message, "denied", "denegado", "access") =>
                "Acceso denegado al leer o escribir un archivo.",
            IOException =>
                "Error de lectura o escritura en disco o red.",
            _ => LimpiarMensajeTecnico(actual.Message)
        };
    }

    /// <summary>Mejora líneas de registro ERROR/WARN antes de mostrarlas en la GUI.</summary>
    public static string TraducirMensajeRegistro(string nivel, string mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje))
        {
            return mensaje;
        }

        if (Contiene(mensaje, "UnauthorizedAccessException", "Access to the path is denied"))
        {
            return "Sin permisos para acceder a un archivo o carpeta. "
                   + "Prueba a ejecutar SManager como administrador o revisa los permisos.";
        }

        if (Contiene(mensaje, "Cola de copia llena", "Cola llena"))
        {
            return "Hay demasiados archivos pendientes de copia. El motor esperará y reintentará.";
        }

        if (Contiene(mensaje, "Error grave en bucle principal", "Error en el ciclo de control"))
        {
            return mensaje;
        }

        if (string.Equals(nivel, "ERROR", StringComparison.OrdinalIgnoreCase)
            && Contiene(mensaje, "Exception"))
        {
            return "Se produjo un error durante la sincronización. Consulta el detalle técnico en el tooltip.";
        }

        return mensaje;
    }

    private static string LimpiarMensajeTecnico(string mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje))
        {
            return "Error desconocido.";
        }

        var recortado = mensaje.Trim();
        if (recortado.Length > 240)
        {
            recortado = recortado[..240] + "…";
        }

        return recortado;
    }

    private static bool Contiene(string texto, params string[] fragmentos)
    {
        foreach (var fragmento in fragmentos)
        {
            if (texto.Contains(fragmento, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
