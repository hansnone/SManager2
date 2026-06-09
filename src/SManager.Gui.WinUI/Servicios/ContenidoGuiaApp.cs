using SManager.Gui.WinUI.Models;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Texto de la guía de referencia mostrada en la sección Guía de la GUI.</summary>
public static class ContenidoGuiaApp
{
    public static IReadOnlyList<SeccionGuiaViewModel> ObtenerSecciones() =>
    [
        new()
        {
            Titulo = "1. ¿Qué es SManager 2.0?",
            Cuerpo =
                "SManager 2.0 sincroniza archivos en una sola dirección: desde una carpeta origen hacia una carpeta destino. "
                + "No borra en destino lo que desaparece del origen; copia y actualiza lo que detecta.\n\n"
                + "Componentes:\n"
                + "• GUI (esta aplicación): configuración, control y visualización.\n"
                + "• Demonio (SManager.Host.exe): motor que vigila y copia en segundo plano.\n"
                + "• CLI (smanager.exe): mismos comandos desde terminal o scripts.\n\n"
                + "La GUI y la CLI comparten el mismo demonio y los mismos datos en %LOCALAPPDATA%\\SManager2."
        },
        new()
        {
            Titulo = "2. Conceptos: perfil, par y demonio",
            Cuerpo =
                "Perfil: conjunto aislado de configuración, log y telemetría. Puedes tener varios (p. ej. «General», «Trabajo», «Estres»).\n\n"
                + "Par de sincronización: una regla origen → destino con nombre, filtros y estado (activo, pausado, inactivo).\n\n"
                + "Demonio: proceso que ejecuta la sincronización. Mientras está activo, la configuración en la GUI queda bloqueada; "
                + "usa Recargar para aplicar un JSON editado fuera de la app, o Detener → editar → Guardar → Iniciar."
        },
        new()
        {
            Titulo = "3. Primeros pasos (flujo recomendado)",
            Cuerpo =
                "1. En el panel lateral, elige o crea un perfil.\n"
                + "2. Ve a Sincronización → Nuevo par → define origen, destino y filtros.\n"
                + "3. Pulsa Guardar (o Ctrl+S). La ruta del JSON aparece en la barra superior.\n"
                + "4. Pulsa Iniciar. El indicador pasa a «Sincronizando» (verde).\n"
                + "5. Revisa Monitor, Registro y Estadísticas.\n\n"
                + "Requisitos para Iniciar:\n"
                + "• Al menos un par habilitado (activo).\n"
                + "• Rutas de origen y destino existentes y accesibles.\n"
                + "• Iniciar guarda automáticamente la configuración antes de lanzar el demonio."
        },
        new()
        {
            Titulo = "4. Barra superior y atajos",
            Cuerpo =
                "Iniciar: lanza el demonio del perfil activo (guarda antes).\n"
                + "Detener: apagado ordenado del demonio.\n"
                + "Recargar (F5): relee el JSON en disco sin reiniciar el proceso (solo con demonio activo).\n"
                + "Guardar (Ctrl+S): escribe la configuración actual en el JSON del perfil.\n\n"
                + "Indicador de estado:\n"
                + "• Rojo «Detenido»: no hay demonio en ejecución.\n"
                + "• Verde «Sincronizando»: demonio activo.\n"
                + "• «• sin guardar» en la ruta: hay cambios en memoria no escritos en disco.\n\n"
                + "Al cambiar de perfil o cerrar la app con cambios sin guardar, se pregunta: Guardar, Descartar o Cancelar."
        },
        new()
        {
            Titulo = "5. Perfiles",
            Cuerpo =
                "Selector de perfil (panel lateral): cambia el contexto de toda la aplicación.\n\n"
                + "Nuevo perfil: crea carpeta y configuración por defecto en Perfiles configuracion.\n\n"
                + "Eliminar perfil (requiere demonio detenido):\n"
                + "• Borra datos en %LOCALAPPDATA%\\SManager2 del perfil (IPC, log, configuración por defecto).\n"
                + "• No borra archivos ya copiados en las carpetas destino.\n"
                + "• Si usas JSON personalizado, puedes conservar ese archivo en disco.\n\n"
                + "CLI equivalente: smanager perfil eliminar -perfil \"Nombre\" [-EliminarJsonPersonalizado]"
        },
        new()
        {
            Titulo = "6. Sincronización — pares",
            Cuerpo =
                "Cada par define:\n"
                + "• Nombre: etiqueta legible.\n"
                + "• Origen / Destino: carpetas locales o de red accesibles.\n"
                + "• Incluir: patrones glob separados por ; (* = todo).\n"
                + "• Excluir: patrones a ignorar (p. ej. ~$*;*.tmp;*.partial;*.lnk).\n"
                + "• Activo: si está deshabilitado, el par se ignora.\n"
                + "• Pausado: el par sigue en la config pero no sincroniza hasta reactivarlo.\n\n"
                + "Validar rutas: comprueba que las carpetas de pares habilitados existen (no guarda en disco).\n\n"
                + "Quitar par: elimina de la configuración en memoria; pulsa Guardar para persistir."
        },
        new()
        {
            Titulo = "7. Filtros (patrones glob)",
            Cuerpo =
                "Los filtros usan comodines estilo glob de Windows:\n"
                + "• * coincide con cualquier secuencia.\n"
                + "• ? coincide con un carácter.\n"
                + "• Varios patrones se separan con punto y coma (;).\n\n"
                + "Ejemplos:\n"
                + "• Incluir * → todos los archivos.\n"
                + "• Incluir *.pdf;*.docx → solo esas extensiones.\n"
                + "• Excluir ~$* → archivos temporales de Office.\n"
                + "• Excluir *.tmp;Thumbs.db → temporales y miniaturas.\n\n"
                + "La exclusión se aplica después de la inclusión."
        },
        new()
        {
            Titulo = "8. Monitor en tiempo real",
            Cuerpo =
                "Tres paneles apilados con separadores arrastrables:\n\n"
                + "Estado por par: nombre, estado de sincronización, copiados y errores por par.\n"
                + "Copiando ahora: archivos en curso con progreso, ETA y barra.\n"
                + "Actividad reciente: últimos eventos (hora, tipo, archivo, par).\n\n"
                + "• Arrastra las barras grises entre paneles para cambiar su altura.\n"
                + "• Cada lista tiene scroll vertical independiente.\n"
                + "• Las alturas se recuerdan entre sesiones.\n\n"
                + "Con el demonio detenido, los paneles muestran «sin telemetría en vivo» y se vacían."
        },
        new()
        {
            Titulo = "9. Registro",
            Cuerpo =
                "Muestra el archivo de log del demonio (smanager.log) con colores por nivel.\n\n"
                + "Filtros:\n"
                + "• Par: todos o un par concreto.\n"
                + "• Nivel: INFO, WARN, ERROR, PENDIENTE.\n"
                + "• Buscar: texto libre en el mensaje.\n\n"
                + "El registro se actualiza en vivo con el demonio activo y también se puede leer tras detenerlo. "
                + "Al cambiar de perfil se carga el log correspondiente."
        },
        new()
        {
            Titulo = "10. Estadísticas",
            Cuerpo =
                "Métricas agregadas de la sesión en curso o última instantánea guardada:\n\n"
                + "• Tiempo de sesión, PID, última actualización.\n"
                + "• Datos copiados, velocidad media, archivos copiados/errores.\n"
                + "• Cola pendiente, hidrataciones, copias activas.\n"
                + "• RAM y CPU del demonio (si la telemetría lo incluye).\n"
                + "• Tamaño del log en disco y desglose por par.\n\n"
                + "Sin demonio activo, las métricas en vivo se reinician; el tamaño del log en disco sigue visible."
        },
        new()
        {
            Titulo = "11. Ajustes avanzados",
            Cuerpo =
                "Vigilancia:\n"
                + "• Polling de seguridad (s): barrido periódico por si se perdió un evento del sistema.\n"
                + "• Estabilidad del archivo (s): espera antes de copiar para archivos aún en escritura.\n\n"
                + "Rendimiento:\n"
                + "• Copiadores / Hidratadores en paralelo: hilos de copia e hidratación (OneDrive, etc.).\n"
                + "• Timeout hidratación (s): tiempo máximo esperando que un archivo en la nube esté local.\n"
                + "• Publicación de estado (ms): frecuencia con la que el demonio escribe estado.json.\n\n"
                + "Tras cambiar valores: Guardar y Recargar (o Detener → Iniciar)."
        },
        new()
        {
            Titulo = "12. Archivo de configuración (JSON)",
            Cuerpo =
                "Por defecto cada perfil usa:\n"
                + "%LOCALAPPDATA%\\SManager2\\Perfiles configuracion\\<perfil>\\configuracion.json\n\n"
                + "Opciones en Ajustes:\n"
                + "• Abrir JSON existente: apunta el perfil a un .json en otra ruta.\n"
                + "• Guardar como: copia la config actual a una ruta personalizada.\n"
                + "• Usar ubicación por defecto: vuelve a la carpeta estándar (no borra el JSON personalizado).\n\n"
                + "Con demonio activo no se puede cambiar la ubicación del JSON.\n\n"
                + "CLI:\n"
                + "smanager config [-perfil X]\n"
                + "smanager config set -perfil X -ruta C:\\ruta\\config.json\n"
                + "smanager config reset -perfil X"
        },
        new()
        {
            Titulo = "13. Mantenimiento del perfil",
            Cuerpo =
                "Limpiar datos del perfil (demonio detenido):\n"
                + "• Vacía el log y la telemetría IPC.\n"
                + "• Reinicia contadores copiados/errores en el JSON.\n"
                + "• No elimina pares ni archivos en destino.\n\n"
                + "Eliminar perfil: borra el perfil completo y sus carpetas locales (ver sección Perfiles).\n\n"
                + "Ambas acciones requieren que el demonio esté detenido."
        },
        new()
        {
            Titulo = "14. Línea de comandos (smanager)",
            Cuerpo =
                "Ubicación tras instalar: carpeta herramientas junto a la GUI.\n\n"
                + "Comandos principales:\n"
                + "smanager start [-perfil X] [-configpath ruta.json]\n"
                + "smanager stop [-perfil X]\n"
                + "smanager status [-perfil X]   → exit 0 si RUN, 1 si STOP\n"
                + "smanager reload [-perfil X]\n"
                + "smanager perfiles\n"
                + "smanager help\n\n"
                + "start sin -configpath usa la ruta resuelta del perfil (por defecto o personalizada)."
        },
        new()
        {
            Titulo = "15. Archivos y carpetas en disco",
            Cuerpo =
                "%LOCALAPPDATA%\\SManager2\\\n"
                + "├── Perfiles configuracion\\<perfil>\\configuracion.json\n"
                + "├── Perfiles\\<perfil>\\\n"
                + "│   ├── estado.json      (telemetría para Monitor/Estadísticas)\n"
                + "│   ├── smanager.log     (registro)\n"
                + "│   ├── smanager.pid     (PID del demonio)\n"
                + "│   └── control.json     (comandos pendientes)\n"
                + "├── preferencias_monitor.json\n"
                + "└── gui_crash.log        (diagnóstico si la GUI falla)\n\n"
                + "Preferencias.json (ruta personalizada del JSON) vive en Perfiles\\<perfil>\\ si se configuró."
        },
        new()
        {
            Titulo = "16. Solución de problemas",
            Cuerpo =
                "La GUI se cierra al abrir:\n"
                + "→ Revisa %LOCALAPPDATA%\\SManager2\\gui_crash.log\n"
                + "→ Reinstala desde el instalador más reciente.\n\n"
                + "Iniciar falla o no copia nada:\n"
                + "→ Comprueba que hay al menos un par activo con rutas válidas.\n"
                + "→ Mira el Registro (nivel ERROR).\n\n"
                + "Cambios en la GUI no se aplican al demonio:\n"
                + "→ Pulsa Guardar y luego Recargar (o reinicia el demonio).\n\n"
                + "Monitor con datos viejos tras Detener:\n"
                + "→ Es normal que se vacíe; si no, actualiza a la última versión.\n\n"
                + "Detener tarda:\n"
                + "→ El demonio puede tardar hasta ~90 s en cerrar copias en curso antes de forzar."
        }
    ];
}
