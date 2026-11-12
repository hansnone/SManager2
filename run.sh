#!/usr/bin/env bash
# ==========================================================================
# Script de Instalación 100% Automática, Compilación y Arranque (Bash)
# Archivo: run.sh
# Proyecto: SManager 2.0 (Motor + CLI + Pruebas Automáticas)
# ==========================================================================

# Detiene la ejecución inmediatamente si ocurre un error crítico
set -e

# --- VARIABLES AJUSTABLES ---
NOMBRE_PROYECTO="SManager 2.0"
CONFIGURACION="Release"
PROYECTO_CORE="src/SManager.Core/SManager.Core.csproj"
PROYECTO_IPC="src/SManager.Ipc/SManager.Ipc.csproj"
PROYECTO_SHARED="src/SManager.Gui.Shared/SManager.Gui.Shared.csproj"
PROYECTO_TESTS="tests/SManager.Core.Tests/SManager.Core.Tests.csproj"

echo "===================================================="
echo " Iniciando automáticamente: $NOMBRE_PROYECTO"
echo " Sistema Operativo: $(uname -s)"
echo " Configuración: $CONFIGURACION"
echo "===================================================="

# --- 1. VERIFICACIÓN DE .NET SDK ---
echo "[1/3] Verificando .NET SDK..."
if [ -d "$HOME/.dotnet/sdk" ]; then
    export DOTNET_ROOT="$HOME/.dotnet"
    export PATH="$HOME/.dotnet:$PATH"
fi
if ! command -v dotnet &> /dev/null; then
    echo "[ERROR] No se encontró 'dotnet'. Instala .NET 8 SDK para continuar."
    exit 1
fi

# --- 2. RESTAURACIÓN Y COMPILACIÓN AUTOMÁTICA ---
echo "[2/3] Restaurando paquetes NuGet y compilando librerías principales..."
dotnet restore "$PROYECTO_CORE" --nologo -v q
dotnet restore "$PROYECTO_IPC" --nologo -v q
dotnet restore "$PROYECTO_SHARED" --nologo -v q
dotnet restore "$PROYECTO_TESTS" --nologo -v q

dotnet build "$PROYECTO_CORE" -c "$CONFIGURACION" --no-restore --nologo -v q
dotnet build "$PROYECTO_IPC" -c "$CONFIGURACION" --no-restore --nologo -v q
dotnet build "$PROYECTO_SHARED" -c "$CONFIGURACION" --no-restore --nologo -v q

# --- 3. EJECUCIÓN DE PRUEBAS UNITARIAS O ARRANQUE EN WINDOWS (MINGW/CYGWIN) ---
echo "[3/3] Ejecutando verificación automática de pruebas..."
dotnet test "$PROYECTO_TESTS" -c "$CONFIGURACION" --nologo

if [[ "$(uname -s)" == MINGW* ]] || [[ "$(uname -s)" == MSYS* ]] || [[ "$(uname -s)" == CYGWIN* ]]; then
    if [ "${1:-}" != "--test" ] && [ "${1:-}" != "--build-only" ]; then
        echo "Entorno Windows detectado en Bash: delegando arranque de GUI a run.bat..."
        cmd.exe //c run.bat "$@"
    fi
fi

echo "[LISTO] Proceso completado con éxito."
