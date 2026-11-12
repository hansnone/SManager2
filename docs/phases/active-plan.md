# Plan Activo: Fases A + B + Vista Previa Detallada de Archivos (SManager 2.0)

## 1. Resumen Claro
Este plan consolida las mejoras acordadas para dejar **SManager 2.0** sólido, seguro, bien documentado en español y con una experiencia de usuario impecable:
- **Corrección de fallos críticos y resiliencia (Fase A):** Completar la señal IPC `AutorizarPurgaEspejo`, guardar y cargar correctamente el `Modo` de sincronización en la interfaz gráfica, proteger el Modo Espejo ante eventos `Deleted` en tiempo real para que nunca se salte el Guardián Antidesastre, reconectar automáticamente `FileSystemWatcher` tras microcortes de red/USB, conservar la fecha original de modificación (`LastWriteTimeUtc`) de los archivos copiados, corregir la comparación de tamaños y sacar las comprobaciones de red (`Directory.Exists`) del hilo de la interfaz para evitar bloqueos.
- **Calidad UX/UI, modularidad y automatización (Fase B):** Retirar el proyecto antiguo WinForms (`SManager.Gui`) de la solución para dejar únicamente la interfaz moderna `SManager.Gui.WinUI`, aplicar la paleta sobria Azul Marino (`#0F2942`) inspirada en `themes/sober-light.css`, pulir todos los textos a *Sentence case* en español sin rastro de IA, dividir `MainPageViewModel.cs` en clases parciales legibles y educativas (<500 líneas por archivo), permitir editar y recargar en caliente, y configurar `run.bat` y `run.sh` para compilación y arranque 100 % automáticos.
- **Vista previa detallada de archivos en «Analizar cambios» (Fase C seleccionada):** Ampliar el analizador de cambios y su ventana en la GUI para mostrar, además del resumen numérico, el listado detallado de los archivos concretos que se van a copiar (nuevos), sobrescribir (modificados) o eliminar en destino (huérfanos en Modo Espejo), con su ruta relativa, par, acción y tamaño, más soporte de filtros por subcarpeta (`node_modules`, `.git`, etc.).

## 2. Suposiciones y Limitaciones
- **Herramientas asumidas:** .NET 8 SDK instalado en el sistema; Windows 10 (19041+) o Windows 11 de 64 bits para compilar y ejecutar la aplicación WinUI 3 (`SManager.Gui.WinUI`). En Linux/macOS (`run.sh`), .NET 8 permite compilar y ejecutar las pruebas unitarias (`SManager.Core.Tests`), el motor (`SManager.Core`/`SManager.Host`) y la CLI (`SManager.Cli`).
- **Límite en la vista previa detallada:** Para no saturar la memoria ni la interfaz cuando una carpeta tiene decenas de miles de archivos pendientes, la lista detallada en pantalla mostrará hasta un máximo configurable (por defecto los primeros 500 archivos por categoría: nuevos, modificados y purgas espejo), indicando siempre el recuento total exacto.

## 3. Pasos de Desarrollo (10 Tareas Atómicas)

1. **Completar el protocolo IPC para `AutorizarPurgaEspejo`**
   - **Archivo a editar:** `src/SManager.Ipc/ServicioIpc.cs`
   - **Acción:** Añadir el mapeo `ComandoControl.AutorizarPurgaEspejo => "AUTORIZAR_PURGA_ESPEJO"` en `EnviarComandoAsync` y `"AUTORIZAR_PURGA_ESPEJO" => ComandoControl.AutorizarPurgaEspejo` en `LeerComandoPendienteAsync`, con comentarios educativos en español.

2. **Preservar fechas de modificación (`LastWriteTimeUtc`), corregir `ComparadorSincronizacion` y soportar filtros por carpeta**
   - **Archivos a editar:** `src/SManager.Core/Copia/ServicioCopia.cs`, `src/SManager.Core/Copia/ComparadorSincronizacion.cs`, `src/SManager.Core/Utilidades/ServicioFiltros.cs`
   - **Acción:**
     - En `ServicioCopia.CopiarArchivoAtomico`, aplicar `File.SetLastWriteTimeUtc(rutaTemporal, mtimeOrigenUtc)` antes de renombrar al archivo final para conservar la fecha original de fotos y documentos.
     - En `ComparadorSincronizacion.DifiereDeDestino`, devolver `true` siempre que `tamanoOrigen != tamanoDestino` o cuando `mtimeOrigenUtc - mtimeDestinoUtc > ToleranciaSegundosMtime`. Validar además el separador de directorio en `ObtenerRutaRelativa` para evitar falsos positivos con prefijos de carpetas.
     - En `ServicioFiltros.PasaFiltros`, aceptar opcionalmente la ruta relativa para que reglas de exclusión de carpetas (como `node_modules` o `.git`) excluyan cualquier archivo dentro de esos segmentos de directorio.

3. **Blindar `VigiaPar` en Modo Espejo y reconectar `FileSystemWatcher` ante fallos de red/USB**
   - **Archivo a editar:** `src/SManager.Core/Vigia/VigiaPar.cs`
   - **Acción:**
     - Suscribir `_vigilante.Error` para liberar el watcher averiado, registrar un aviso claro y forzar su recreación junto con un escaneo diferencial en el siguiente ciclo.
     - En los manejadores de `FileSystemWatcher`, consultar siempre `ObtenerPar()` en vivo en lugar de capturar una referencia antigua en la clausura.
     - Reemplazar el borrado recursivo inmediato en `_vigilante.Deleted` por una petición de escaneo diferencial (`_estado.PeticionEscaneoCompleto[_idPar] = true`), de modo que toda eliminación en Modo Espejo pase obligatoriamente por el **Guardián Antidesastre (`UmbralPurgaMasivaEspejo`)** antes de borrar archivos en destino.

4. **Ampliar el modelo y servicio de «Analizar cambios» con vista previa detallada de archivos**
   - **Archivos a editar:** `src/SManager.Gui.Shared/Modelos/ResultadoAnalisisCambios.cs`, `src/SManager.Gui.Shared/Servicios/ServicioAnalisisCambios.cs`
   - **Acción:**
     - Añadir el registro `ArchivoCambioPrevisto(string NombrePar, string RutaRelativa, string TipoAccion, long TamanoBytes)` (`"Nuevo"`, `"Modificado"`, `"Eliminar (Espejo)"`) y la colección `ArchivosPrevistos` en `ResultadoAnalisisPar` y `ResultadoAnalisisGlobal` (con límite de seguridad de 500 elementos detallados por par para mantener fluidez).
     - En `ServicioAnalisisCambios.AnalizarPar`, recopilar los archivos nuevos, los modificados y, si el par está en `ModoSincronizacion.Espejo`, también los archivos huérfanos en destino que serían eliminados.

5. **Evitar bloqueos del hilo UI en comprobaciones de red (`ServicioMonitorNotificaciones`)**
   - **Archivo a editar:** `src/SManager.Gui.WinUI/Servicios/ServicioMonitorNotificaciones.cs`
   - **Acción:** Reemplazar la llamada síncrona `Directory.Exists(ruta)` en cada tick de 500 ms por una caché de accesibilidad verificada de forma asíncrona en segundo plano cada 10 segundos con timeout, impidiendo que una ruta UNC (`\\NAS\...`) caída congele la ventana principal.

6. **Persistir `Modo` y `UmbralPurgaMasivaEspejo` en la GUI y dividir `MainPageViewModel` en clases parciales (<500 líneas)**
   - **Archivos a editar y crear:**
     - `src/SManager.Gui.WinUI/Models/ParFilaViewModel.cs`
     - `src/SManager.Gui.WinUI/Vistas/DialogoParControl.xaml` y `src/SManager.Gui.WinUI/Vistas/DialogoParControl.xaml.cs`
     - `src/SManager.Gui.WinUI/ViewModels/MainPageViewModel.cs` (núcleo y propiedades)
     - `src/SManager.Gui.WinUI/ViewModels/MainPageViewModel.Perfiles.cs` (gestión de perfiles y rutas JSON)
     - `src/SManager.Gui.WinUI/ViewModels/MainPageViewModel.Pares.cs` (gestión de pares, edición en caliente + auto-recarga y ventana de «Análisis de cambios» con tabla detallada de archivos)
     - `src/SManager.Gui.WinUI/ViewModels/MainPageViewModel.Telemetria.cs` (monitor, registro, estadísticas e historial)
   - **Acción:**
     - Incluir `Modo`, `BorrarEnOrigen` y `UmbralPurgaMasivaEspejo` en `CargarConfiguracionPerfilActualInterno()`, `LeerConfiguracionDesdeUi()`, `CopiarDatosPar()` y en `DialogoParControl`.
     - Permitir editar pares con el demonio en marcha: al guardar cambios de un par con el demonio activo, guardar el JSON y enviar `ComandoControl.Recargar` automáticamente.
     - Actualizar `MostrarDialogoAnalisisAsync` para mostrar el desplegable con la lista detallada de archivos (acción, ruta relativa, par y tamaño) y un botón directo **«Sincronizar ahora»** / navegación real a la sección Sincronización.

7. **Aplicar tema Azul Marino (`#0F2942`), estados vacíos del Monitor y UX Writing (*Sentence case*)**
   - **Archivos a editar:** `src/SManager.Gui.WinUI/App.xaml`, `src/SManager.Gui.WinUI/Recursos/OrigenDestinoTema.xaml`, `src/SManager.Gui.WinUI/Recursos/EstadoParTema.xaml`, `src/SManager.Gui.WinUI/MainPage.xaml`, `src/SManager.Gui.WinUI/MainPage.xaml.cs`, `src/SManager.Gui.WinUI/Servicios/ContenidoGuiaApp.cs`, `README.md`
   - **Acción:**
     - Incorporar en los diccionarios de recursos la paleta sobria Azul Marino (`#0F2942` acento principal, `#243B53` secundario, `#1B4332` éxito, `#9C4221` advertencia, `#9B2C2C` error) alineada con `themes/sober-light.css`.
     - Añadir estados vacíos naturales en las listas *"Copiando ahora"* (`"No hay copias en curso en este momento"`) y *"Actividad reciente"* (`"Aún no se han registrado eventos en esta sesión"`) del panel Monitor.
     - Corregir todos los textos en *Title Case* anglosajón a *Sentence case* español natural (`"Purga masiva detenida"`, `"Autorizar purga"`, `"Autenticación confirmada"`, `"Acceso denegado"`, `"Pares iniciados"`, `"requiere administrador local"`).
     - Actualizar `ContenidoGuiaApp.cs` y `README.md` explicando con claridad los 3 modos de sincronización.

8. **Retirar el proyecto legado WinForms (`SManager.Gui`) de la solución**
   - **Archivo a editar:** `SManager2.slnx`
   - **Acción:** Eliminar la referencia a `src/SManager.Gui/SManager.Gui.csproj` de `SManager2.slnx` para dejar únicamente la interfaz moderna `SManager.Gui.WinUI` como aplicación gráfica oficial.

9. **Configurar `run.bat` y `run.sh` en modo 100 % automático**
   - **Archivos a editar:** `run.bat`, `run.sh`
   - **Acción:**Actualizar ambos scripts con variables claras y documentadas en español para verificar `.NET 8`, restaurar dependencias sin pedir confirmación (`dotnet restore`), ejecutar las pruebas o compilar y lanzar `SManager.Gui.WinUI` (en Windows) o `SManager.Cli` / tests (en Linux/macOS).

10. **Ampliar pruebas automáticas y verificar estándares**
    - **Archivos a editar:** `tests/SManager.Core.Tests/ComparadorSincronizacionTests.cs`, `tests/SManager.Core.Tests/ModosSincronizacionTests.cs`
    - **Acción:** Añadir pruebas unitarias para:
      1. Preservación de `LastWriteTimeUtc` tras copiar y detección de cambios cuando solo difiere el tamaño.
      2. Exclusión de subcarpetas (`node_modules`, `.git`) en `ServicioFiltros`.
      3. Serialización/lectura de `ComandoControl.AutorizarPurgaEspejo` en `ServicioIpc`.
      4. Vista previa detallada de archivos en `ServicioAnalisisCambios` (nuevos, modificados y huérfanos en Modo Espejo).

## 4. Diseño de Datos e Interfaz

### Estructuras de Datos Nuevas/Actualizadas (`ResultadoAnalisisCambios.cs`)
```csharp
// Representa un archivo individual detectado durante la simulación ("Analizar cambios")
public sealed record ArchivoCambioPrevisto(
    string NombrePar,
    string RutaRelativa,
    string TipoAccion, // "Nuevo", "Modificado" o "Eliminar (Espejo)"
    long TamanoBytes,
    string TamanoFormateado);
```

### Estilo Visual y los 4 Estados Visuales
- **Paleta Sobria Azul Marino (`themes/sober-light.css` → XAML):**
  - Acento principal y botones primarios (`SystemAccentColor` / `OrigenAcentoBrush`): `#0F2942` (hover `#081827`, secundario `#243B53`).
  - Fondos claros: `#F4F6F8` (base) y `#FFFFFF` (tarjetas), bordes `#D9E2EC`, texto principal `#102A43` y secundario `#486581`.
  - Estados semánticos sobrios: Éxito `#1B4332`, Advertencia `#9C4221`, Error `#9B2C2C`.
- **Los 4 Estados en «Analizar cambios» y paneles:**
  - **Cargando:** `ProgressRing` activo junto al botón `"Analizar cambios"` con texto de estado `"Analizando archivos..."`.
  - **Vacío:** Si no hay diferencias entre origen y destino, mensaje claro en la vista previa: `"Todo está al día. No hay archivos nuevos ni modificados pendientes de copiar."` En Monitor, mensajes naturales cuando no hay copias activas ni actividad reciente.
  - **Error:** `InfoBar` de advertencia/error con explicación humana si alguna carpeta de origen o destino no existe o no tiene permisos de lectura.
  - **Éxito:** Tabla de resumen + desplegable (`Expander`) `"Ver detalle de archivos"` con las columnas **Acción**, **Archivo (ruta relativa)**, **Par** y **Tamaño**, y botón principal `"Sincronizar ahora"`.

## 5. Automatización (`run.bat` y `run.sh`)
- **`run.bat` (Windows):**
  1. Comprueba `where dotnet`.
  2. Ejecuta `dotnet restore SManager2.slnx --nologo -v q`.
  3. Compila en paralelo `SManager.Host`, `SManager.Cli` y `SManager.Gui.WinUI` para que la GUI disponga de los ejecutables del demonio y CLI actualizados.
  4. Lanza `dotnet run --project src\SManager.Gui.WinUI\SManager.Gui.WinUI.csproj` (o `dotnet test` si se invoca `run.bat test`).
- **`run.sh` (Linux / macOS):**
  1. Comprueba `command -v dotnet`.
  2. Restaura y compila `SManager.Core`, `SManager.Ipc`, `SManager.Host`, `SManager.Cli` y `SManager.Core.Tests`.
  3. Ejecuta las pruebas unitarias (`dotnet test tests/SManager.Core.Tests/SManager.Core.Tests.csproj`) y muestra la ayuda interactiva de `SManager.Cli`.

## 6. Plan de Verificación y Paths Permitidos
- **Comandos de verificación:**
  - `dotnet test tests/SManager.Core.Tests/SManager.Core.Tests.csproj`
  - `dotnet build src/SManager.Gui.WinUI/SManager.Gui.WinUI.csproj`
  - `python .agents/scripts/verify-code.py`
- **Paths permitidos:**
  - `SManager2.slnx`, `run.bat`, `run.sh`, `README.md`, `docs/phases/active-plan.md`
  - `src/SManager.Core/**`
  - `src/SManager.Ipc/**`
  - `src/SManager.Host/**`
  - `src/SManager.Cli/**`
  - `src/SManager.Gui.Shared/**`
  - `src/SManager.Gui.WinUI/**`
  - `tests/SManager.Core.Tests/**`
