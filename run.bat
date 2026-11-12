@echo off
REM ==========================================================================
REM Script de Instalacion 100% Automatica, Compilacion y Arranque (Windows)
REM Archivo: run.bat
REM Proyecto: SManager 2.0 (Motor + CLI + Interfaz WinUI 3)
REM ==========================================================================

setlocal EnableDelayedExpansion

REM --- VARIABLES AJUSTABLES ---
set NOMBRE_PROYECTO=SManager 2.0
set CONFIGURACION=Release
set PLATAFORMA=x64
set PROYECTO_HOST=src\SManager.Host\SManager.Host.csproj
set PROYECTO_CLI=src\SManager.Cli\SManager.Cli.csproj
set PROYECTO_GUI=src\SManager.Gui.WinUI\SManager.Gui.WinUI.csproj
set PROYECTO_TESTS=tests\SManager.Core.Tests\SManager.Core.Tests.csproj

echo ====================================================
echo  Iniciando automaticamente: %NOMBRE_PROYECTO%
echo  Sistema Operativo: Windows (%PLATAFORMA%)
echo  Configuracion: %CONFIGURACION%
echo ====================================================

REM --- 1. VERIFICAR .NET SDK ---
echo [1/4] Verificando .NET SDK...
if exist "%USERPROFILE%\.dotnet\sdk" (
    set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
    set "PATH=%USERPROFILE%\.dotnet;%PATH%"
)
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo [ERROR] No se encontro 'dotnet'. Instala .NET 8 SDK en Windows para continuar.
    pause
    exit /b 1
)

REM --- 2. RESTAURAR PAQUETES NUGET AUTOMATICAMENTE ---
echo [2/4] Restaurando dependencias y paquetes NuGet automaticamente...
dotnet restore "%PROYECTO_HOST%" --nologo -v q
if %errorlevel% neq 0 goto :error
dotnet restore "%PROYECTO_CLI%" --nologo -v q
if %errorlevel% neq 0 goto :error
dotnet restore "%PROYECTO_GUI%" -p:Platform=%PLATAFORMA% --nologo -v q
if %errorlevel% neq 0 goto :error

REM --- 3. COMPILAR MOTOR (HOST), CLI Y GUI WINUI ---
echo [3/4] Compilando motor (SManager.Host), CLI (smanager) e interfaz (SManager.Gui.WinUI)...
dotnet build "%PROYECTO_HOST%" -c %CONFIGURACION% --no-restore --nologo -v q
if %errorlevel% neq 0 goto :error

dotnet build "%PROYECTO_CLI%" -c %CONFIGURACION% --no-restore --nologo -v q
if %errorlevel% neq 0 goto :error

dotnet build "%PROYECTO_GUI%" -c %CONFIGURACION% -p:Platform=%PLATAFORMA% --no-restore --nologo -v q
if %errorlevel% neq 0 goto :error

REM Si se pasa el argumento --test, ejecuta la bateria de pruebas automaticas y termina
if /I "%~1"=="--test" (
    echo [4/4] Ejecutando bateria de pruebas unitarias...
    dotnet test "%PROYECTO_TESTS%" -c %CONFIGURACION% --nologo
    goto :fin
)

REM Si se pasa el argumento --build-only, termina tras compilar sin abrir ventana
if /I "%~1"=="--build-only" (
    echo [4/4] Compilacion completada correctamente ^(modo --build-only^).
    goto :fin
)

REM --- 4. ARRANCAR LA INTERFAZ GRAFICA ---
echo [4/4] Iniciando la interfaz grafica SManager 2.0...
set EXE_GUI=src\SManager.Gui.WinUI\bin\%PLATAFORMA%\%CONFIGURACION%\net8.0-windows10.0.19041.0\win-%PLATAFORMA%\SManager.Gui.WinUI.exe
if not exist "%EXE_GUI%" (
    set EXE_GUI=src\SManager.Gui.WinUI\bin\%CONFIGURACION%\net8.0-windows10.0.19041.0\win-%PLATAFORMA%\SManager.Gui.WinUI.exe
)

if exist "%EXE_GUI%" (
    start "" "%EXE_GUI%"
    echo [LISTO] SManager 2.0 se ha iniciado correctamente.
) else (
    dotnet run --project "%PROYECTO_GUI%" -c %CONFIGURACION% -p:Platform=%PLATAFORMA% --no-build
)
goto :fin

:error
echo.
echo [ERROR] El proceso de restauracion o compilacion fallo. Revisa el mensaje anterior.
pause
exit /b 1

:fin
endlocal
