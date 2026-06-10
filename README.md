# SManager 2.0

Sincronización unidireccional **origen → destino** para Windows. Vigila carpetas, copia archivos de forma fiable y ofrece telemetría en tiempo real mediante una interfaz gráfica (WinUI), una línea de comandos y un demonio en segundo plano.

## Para qué sirve

SManager mantiene una **copia actualizada** de una carpeta en otra ubicación: disco externo, NAS, unidad de red o carpeta de backup en el mismo PC.

Casos de uso habituales:

- Copiar fotos y vídeos a un disco USB.
- Replicar documentos de trabajo en un servidor o NAS.
- Mantener una carpeta de proyecto en un segundo disco.
- Automatizar copias con la CLI (`smanager.exe`) en scripts o tareas programadas.

## Qué NO hace

- **No** es sincronización bidireccional (no fusiona dos carpetas activas).
- **No** es un servicio de nube por sí solo (tú defines origen y destino).
- **No** elimina en destino archivos que desaparezcan del origen.
- **No** sustituye un sistema de backup con historial de versiones.

## Interfaz (capturas)

> Añade capturas en [`docs/screenshots/`](docs/screenshots/README.md) y descomenta las líneas siguientes en este README.

<!--
![Panel de control](docs/screenshots/01-inicio.png)
![Asistente de configuración](docs/screenshots/02-asistente.png)
![Analizar cambios](docs/screenshots/04-analizar-cambios.png)
-->

## Características

- Varios **perfiles** independientes (cada uno con su configuración, log y telemetría).
- **Pares de sincronización** con filtros de inclusión/exclusión (patrones glob).
- Motor con cola de copia, hidratación de archivos en la nube, deduplicación y polling de seguridad.
- **GUI** con asistente de primer uso, monitor en vivo, registro filtrable, estadísticas y ajustes avanzados.
- **Modo básico / avanzado**, tema claro/oscuro, historial de sesiones y atajos de teclado.
- **CLI** (`smanager.exe`) para automatización e integración con scripts.
- Instalador único **self-contained** (no requiere instalar .NET ni Windows App SDK).

## Requisitos

| Contexto | Requisito |
|----------|-----------|
| Usuario final (instalador) | Windows 10 19041+ (64 bits) |
| Desarrollo / compilación | .NET SDK 8, Windows 10/11, Visual Studio 2022 o `dotnet` CLI |
| Generar instalador | Inno Setup 6 (`ISCC.exe`) |

## Instalación (usuario final)

1. Ejecuta `SManager2-Setup-2.0.0.exe` (o la versión que tengas en `dist\`).
2. Sigue el asistente (instalación por usuario, sin administrador).
3. Abre **SManager 2.0** desde el menú Inicio.

Los datos de la aplicación se guardan en:

`%LOCALAPPDATA%\SManager2\`

## Uso rápido

**Primera vez (recomendado):**

1. Abre la GUI — se mostrará el **asistente de configuración** si no hay pares.
2. Elige una plantilla (fotos, documentos, trabajo…) y las carpetas origen y destino.
3. Revisa la **vista previa** con «Analizar cambios».
4. Crea el par y pulsa **Iniciar**.

**Uso manual:**

1. Abre la GUI y elige o crea un **perfil**.
2. En **Sincronización**, define pares origen → destino (o usa el asistente desde Inicio).
3. Opcional: **Analizar cambios** antes de copiar.
4. Pulsa **Guardar** (Ctrl+S).
5. Pulsa **Iniciar** para lanzar el demonio.
6. Sigue el progreso en **Inicio**, **Monitor**, **Registro** y **Estadísticas**.

La documentación completa está en la sección **Guía** dentro de la aplicación.

### Modo básico vs avanzado

Por defecto la GUI usa **modo básico** (Inicio, Sincronización, Guía). Activa **modo avanzado** en Ajustes → Interfaz para ver Monitor, Registro, Estadísticas y parámetros del motor.

### Atajos de teclado

| Atajo | Acción |
|-------|--------|
| Ctrl+S | Guardar configuración |
| Ctrl+I | Iniciar sincronización |
| Ctrl+Shift+S | Detener demonio |
| Ctrl+Shift+A | Analizar cambios |
| F5 | Recargar demonio |

## Línea de comandos

La CLI se instala en `%LOCALAPPDATA%\Programs\SManager2\herramientas\`. El instalador añade esa carpeta al **PATH del usuario** (no requiere administrador). Abre una **terminal nueva** tras instalar para que PowerShell reconozca `smanager`.

```powershell
smanager help
smanager start -perfil "General"
smanager status -perfil "General"
smanager reload -perfil "General"
smanager stop -perfil "General"
smanager perfiles
smanager config -perfil "General"
```

## Compilar desde el código fuente

```powershell
# GUI + CLI + demonio (Release)
dotnet publish src\SManager.Gui.WinUI\SManager.Gui.WinUI.csproj -c Release -r win-x64
dotnet publish src\SManager.Cli\SManager.Cli.csproj -c Release -r win-x64
dotnet publish src\SManager.Host\SManager.Host.csproj -c Release -r win-x64
```

## Generar el instalador

```powershell
.\tools\Generar-Instalador.ps1
```

Salida: `dist\SManager2-Setup-2.0.0.exe`

## Estructura del repositorio

```
SManager2/
├── src/
│   ├── SManager.Core/        # Modelos y motor de sincronización
│   ├── SManager.Ipc/         # Rutas, estado en disco, IPC
│   ├── SManager.Host/        # Demonio (SManager.Host.exe)
│   ├── SManager.Cli/         # CLI (smanager.exe)
│   ├── SManager.Gui.Shared/  # Lógica compartida con la GUI
│   └── SManager.Gui.WinUI/   # Interfaz gráfica
├── installer/                # Script Inno Setup
├── tools/                    # Scripts de build e instalador
├── scripts/                  # Pruebas (p. ej. estrés)
└── tests/
```

## Rutas importantes

| Elemento | Ubicación por defecto |
|----------|------------------------|
| Configuración del perfil | `%LOCALAPPDATA%\SManager2\Perfiles configuracion\<perfil>\configuracion.json` |
| Log del demonio | `%LOCALAPPDATA%\SManager2\Perfiles\<perfil>\smanager.log` |
| Telemetría (monitor) | `%LOCALAPPDATA%\SManager2\Perfiles\<perfil>\estado.json` |
| JSON personalizado | Ruta elegida en Ajustes → Archivo de configuración |

## Licencia

Este proyecto está bajo la licencia [MIT](LICENSE).
