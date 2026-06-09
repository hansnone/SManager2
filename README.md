# SManager 2.0

Sincronización unidireccional **origen → destino** para Windows. Vigila carpetas, copia archivos de forma fiable y ofrece telemetría en tiempo real mediante una interfaz gráfica (WinUI), una línea de comandos y un demonio en segundo plano.

## Características

- Varios **perfiles** independientes (cada uno con su configuración, log y telemetría).
- **Pares de sincronización** con filtros de inclusión/exclusión (patrones glob).
- Motor con cola de copia, hidratación de archivos en la nube, deduplicación y polling de seguridad.
- **GUI** con monitor en vivo, registro filtrable, estadísticas y ajustes avanzados.
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

1. Abre la GUI y elige o crea un **perfil**.
2. En **Sincronización**, define pares origen → destino.
3. Pulsa **Guardar** (Ctrl+S).
4. Pulsa **Iniciar** para lanzar el demonio.
5. Sigue el progreso en **Monitor**, **Registro** y **Estadísticas**.

La documentación completa de la interfaz está en la sección **Guía** dentro de la aplicación.

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
