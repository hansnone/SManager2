using SManager.Gui.WinUI.Models;

namespace SManager.Gui.WinUI.Servicios;

/// <summary>Texto de la guía de referencia mostrada en la sección Guía de la GUI.</summary>
public static class ContenidoGuiaApp
{
    public static IReadOnlyList<SeccionGuiaViewModel> ObtenerSecciones() =>
    [
        new()
        {
            Titulo = "Guía rápida (5 minutos)",
            Cuerpo =
                "1. Abre el asistente desde Inicio → «Asistente de configuración» (o se muestra solo la primera vez).\n"
                + "2. Elige plantilla, carpeta origen y carpeta destino.\n"
                + "3. Pulsa «Analizar cambios» para ver qué se copiaría sin tocar archivos.\n"
                + "4. Guarda e Inicia la sincronización.\n"
                + "5. Sigue el progreso en Inicio, Monitor y Registro.\n\n"
                + "Recuerda: es sincronización unidireccional (origen → destino). No borra en destino lo que desaparezca del origen."
        },
        new()
        {
            Titulo = "¿Para qué sirve SManager?",
            Cuerpo =
                "Mantiene una copia actualizada de una carpeta en otra ubicación: disco externo, NAS, unidad de red o carpeta de backup en el mismo PC.\n\n"
                + "Casos habituales:\n"
                + "• Copiar fotos a un disco USB.\n"
                + "• Replicar documentos de trabajo en un servidor.\n"
                + "• Mantener una carpeta de proyecto en un segundo disco.\n"
                + "• Automatizar copias con la CLI (smanager.exe)."
        },
        new()
        {
            Titulo = "¿Qué NO hace SManager?",
            Cuerpo =
                "• No es sincronización bidireccional (no fusiona dos carpetas activas).\n"
                + "• No es almacenamiento en la nube por sí solo (copia hacia rutas que tú defines).\n"
                + "• No resuelve conflictos entre dos copias editadas a la vez.\n"
                + "• No sustituye un backup con versionado histórico (salvo que guardes varias copias manualmente).\n"
                + "• No elimina en destino archivos que ya no existan en origen."
        },
        new()
        {
            Titulo = "Preguntas frecuentes",
            Cuerpo =
                "¿Puedo probar sin copiar nada?\n"
                + "→ Sí. Usa «Analizar cambios» en la barra superior o en Inicio.\n\n"
                + "¿Qué pasa si el destino ya tiene archivos?\n"
                + "→ Los archivos con el mismo nombre y ruta relativa pueden sobrescribirse si el origen es más reciente.\n\n"
                + "¿Puedo pausar un par?\n"
                + "→ Sí, en Sincronización → Editar par → Pausado.\n\n"
                + "¿Cómo sé si algo falló?\n"
                + "→ Revisa Registro (filtro ERROR) y el panel de errores en Inicio.\n\n"
                + "¿Dónde están mis datos?\n"
                + "→ %LOCALAPPDATA%\\SManager2\\"
        },
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
                "Opción A — Asistente (recomendado la primera vez):\n"
                + "1. Inicio → «Asistente de configuración».\n"
                + "2. Sigue los pasos: plantilla, origen, destino, vista previa.\n"
                + "3. Crea el par y opcionalmente inicia la sincronización.\n\n"
                + "Opción B — Manual:\n"
                + "1. En el panel lateral, elige o crea un perfil.\n"
                + "2. Ve a Sincronización → Nuevo par → define origen, destino y filtros.\n"
                + "3. Pulsa «Analizar cambios» si quieres una vista previa.\n"
                + "4. Pulsa Guardar (o Ctrl+S). La ruta del JSON aparece en la barra superior.\n"
                + "5. Pulsa Iniciar. El indicador pasa a «Sincronizando» (verde).\n"
                + "6. Revisa Monitor, Registro y Estadísticas.\n\n"
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
                + "Guardar (Ctrl+S): escribe la configuración actual en el JSON del perfil.\n"
                + "Analizar cambios: compara origen y destino sin copiar archivos (vista previa).\n\n"
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
                + "• Buscar: texto libre en el mensaje.\n"
                + "• Filtro rápido: botones Errores, Advertencias, Info, Pendiente.\n\n"
                + "Exportar:\n"
                + "• Exportar registro: guarda las líneas visibles (con filtros) en Descargas.\n"
                + "• Exportar diagnóstico: paquete con telemetría, rutas y últimas líneas del log.\n\n"
                + "El resumen bajo los filtros indica cuántas líneas visibles hay y cuántos ERROR/WARN contienen.\n\n"
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
                "Sistema:\n"
                + "• Inicio con Windows y abrir minimizado (barra de tareas).\n\n"
                + "Bandeja y notificaciones:\n"
                + "• Icono en la bandeja: menú contextual (Abrir, Sincronizar, Detener, Monitor, Salir).\n"
                + "• Minimizar a la bandeja al cerrar: la X oculta la ventana; el demonio sigue activo.\n"
                + "• Notificaciones: avisos al iniciar/detener y si acumulas errores.\n\n"
                + "Vigilancia:\n"
                + "• Polling de seguridad (s): barrido periódico global; cada par puede tener su propio intervalo en Editar par (0 = global).\n"
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
                "Ubicación: %LOCALAPPDATA%\\Programs\\SManager2\\herramientas\\smanager.exe\n"
                + "El instalador añade esa carpeta al PATH del usuario. Tras instalar, abre una terminal nueva.\n\n"
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
                + "├── preferencias_gui.json   (bandeja, notificaciones, onboarding, tema, modo UI)\n"
                + "├── Perfiles\\<perfil>\\historial_sesiones.json\n"
                + "└── gui_crash.log        (diagnóstico si la GUI falla)\n\n"
                + "Preferencias.json (ruta personalizada del JSON) vive en Perfiles\\<perfil>\\ si se configuró."
        },
        new()
        {
            Titulo = "17. Modo básico y modo avanzado",
            Cuerpo =
                "Modo básico (predeterminado para usuarios nuevos):\n"
                + "• Inicio, Sincronización, Guía y Ajustes esenciales.\n"
                + "• Oculta Monitor, Registro y Estadísticas del menú lateral.\n"
                + "• Oculta parámetros de vigilancia, rendimiento y rutas JSON personalizadas.\n\n"
                + "Modo avanzado:\n"
                + "• Muestra todas las secciones y ajustes del motor.\n"
                + "• Actívalo en Ajustes → Interfaz → Modo avanzado.\n\n"
                + "El dashboard de Inicio sigue mostrando estado, progreso y errores en ambos modos."
        },
        new()
        {
            Titulo = "18. Tema, accesibilidad e idioma",
            Cuerpo =
                "Tema:\n"
                + "• Ajustes → Interfaz → Tema: Sistema, Claro u Oscuro.\n"
                + "• Los colores del registro y los chips de estado usan recursos adaptados al tema.\n\n"
                + "Accesibilidad:\n"
                + "• El indicador de estado incluye texto descriptivo para lectores de pantalla.\n"
                + "• Atajos: Ctrl+S Guardar, F5 Recargar, Ctrl+I Iniciar, Ctrl+Shift+S Detener, Ctrl+Shift+A Analizar.\n"
                + "• Los chips muestran texto (Correcta, Con errores…) además del color.\n\n"
                + "Idioma:\n"
                + "• La interfaz está en español. El selector de idioma está preparado para futuras traducciones."
        },
        new()
        {
            Titulo = "19. Historial de sesiones",
            Cuerpo =
                "En Estadísticas (modo avanzado) verás el historial de las últimas sesiones del demonio.\n\n"
                + "Cada entrada registra al detener:\n"
                + "• Fecha de inicio\n"
                + "• Duración\n"
                + "• Archivos copiados, bytes y errores\n"
                + "• Si la sesión fue correcta o tuvo errores\n\n"
                + "El dashboard de Inicio muestra la última sesión completada sin errores cuando el demonio está detenido.\n\n"
                + "Datos en: Perfiles\\<perfil>\\historial_sesiones.json"
        },
        new()
        {
            Titulo = "20. Confirmaciones al editar pares",
            Cuerpo =
                "Al cambiar la carpeta origen o destino de un par existente, SManager pide confirmación antes de guardar.\n\n"
                + "Así reduces el riesgo de apuntar accidentalmente a otra ruta en la próxima sincronización.\n\n"
                + "Otras confirmaciones ya existentes: eliminar par, eliminar perfil, cambios sin guardar al cerrar."
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
