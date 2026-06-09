#Requires -Version 7.0
<#
.SYNOPSIS
    Genera el instalador único SManager2-Setup.exe para usuarios finales.
.DESCRIPTION
    Publica la GUI WinUI, la CLI (smanager) y el demonio (SManager.Host) como
    despliegues self-contained (sin .NET ni Windows App SDK previos) y compila
    el script Inno Setup en un solo ejecutable.

    Requisitos en la máquina de BUILD (no en la del usuario final):
      - .NET SDK 8
      - Inno Setup 6 (ISCC.exe en PATH o en Program Files)

    El usuario final solo ejecuta dist\SManager2-Setup.exe: no instala nada más.
.PARAMETER Version
    Versión del instalador (ej. 2.0.0). Se pasa a Inno Setup como /DMyAppVersion=...
.PARAMETER SoloCompilar
    Omite dotnet publish y reutiliza dist\staging existente.
.EXAMPLE
    .\tools\Generar-Instalador.ps1
.EXAMPLE
    .\tools\Generar-Instalador.ps1 -Version 2.0.1 -SoloCompilar
#>
[CmdletBinding()]
param(
    [string]$Version = '2.0.0',
    [switch]$SoloCompilar
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- Rutas del repositorio ---
$raizRepo = Split-Path -Parent $PSScriptRoot
$carpetaStaging = Join-Path $raizRepo 'dist\staging'
$carpetaHerramientas = Join-Path $carpetaStaging 'herramientas'
$carpetaSalidaInstalador = Join-Path $raizRepo 'dist'
$scriptInno = Join-Path $raizRepo 'installer\SManager2.iss'

$proyectoGui = Join-Path $raizRepo 'src\SManager.Gui.WinUI\SManager.Gui.WinUI.csproj'
$proyectoCli = Join-Path $raizRepo 'src\SManager.Cli\SManager.Cli.csproj'
$proyectoHost = Join-Path $raizRepo 'src\SManager.Host\SManager.Host.csproj'

# --- Localizar el compilador de Inno Setup ---
function Get-RutaIscc {
    $candidatos = @(
        (Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue)?.Source
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    return $candidatos | Select-Object -First 1
}

# --- Publicar un proyecto .NET self-contained win-x64 ---
function Publish-Proyecto {
    param(
        [Parameter(Mandatory)]
        [string]$Proyecto,
        [Parameter(Mandatory)]
        [string]$Destino
    )

    Write-Host "Publicando: $(Split-Path -Leaf $Proyecto) -> $Destino" -ForegroundColor Cyan

    if (Test-Path -LiteralPath $Destino) {
        Remove-Item -LiteralPath $Destino -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Destino -Force | Out-Null

    $argumentos = @(
        'publish', $Proyecto
        '-c', 'Release'
        '-r', 'win-x64'
        '--self-contained', 'true'
        '-o', $Destino
    )

    # WinUI exige plataforma x64 explícita.
    if ($Proyecto -like '*Gui.WinUI*') {
        $argumentos += @('-p:Platform=x64')
    }

    & dotnet @argumentos
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish falló para $Proyecto (código $LASTEXITCODE)."
    }
}

# --- Validaciones previas ---
if (-not (Test-Path -LiteralPath $scriptInno)) {
    throw "No se encuentra el script Inno Setup: $scriptInno"
}

$rutaIscc = Get-RutaIscc
if (-not $rutaIscc) {
    throw @"
No se encuentra ISCC.exe (Inno Setup 6).
Instálalo desde https://jrsoftware.org/isinfo.php y vuelve a ejecutar este script.
"@
}

Write-Host "SManager 2.0 — generación de instalador v$Version" -ForegroundColor Green
Write-Host "Raíz: $raizRepo"
Write-Host "Inno Setup: $rutaIscc"

# --- Fase 1: publicación self-contained ---
if (-not $SoloCompilar) {
    Write-Host "`n=== Fase 1: publicación self-contained ===" -ForegroundColor Yellow

    if (Test-Path -LiteralPath $carpetaStaging) {
        Remove-Item -LiteralPath $carpetaStaging -Recurse -Force
    }

    Publish-Proyecto -Proyecto $proyectoGui -Destino $carpetaStaging
    Publish-Proyecto -Proyecto $proyectoCli -Destino $carpetaHerramientas

    # Host en carpeta temporal y fusión con CLI (comparten runtime .NET).
    $carpetaHostTemporal = Join-Path $raizRepo 'dist\temp-host'
    Publish-Proyecto -Proyecto $proyectoHost -Destino $carpetaHostTemporal
    Copy-Item -Path (Join-Path $carpetaHostTemporal '*') -Destination $carpetaHerramientas -Recurse -Force
    Remove-Item -LiteralPath $carpetaHostTemporal -Recurse -Force

    # Comprobación mínima: los tres ejecutables principales deben existir.
    $ejecutablesRequeridos = @(
        (Join-Path $carpetaStaging 'SManager.Gui.WinUI.exe')
        (Join-Path $carpetaHerramientas 'smanager.exe')
        (Join-Path $carpetaHerramientas 'SManager.Host.exe')
    )
    foreach ($exe in $ejecutablesRequeridos) {
        if (-not (Test-Path -LiteralPath $exe)) {
            throw "Falta el ejecutable esperado tras publish: $exe"
        }
    }

    $tamanoMb = [math]::Round(
        ((Get-ChildItem -LiteralPath $carpetaStaging -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB),
        1
    )
    Write-Host "Staging listo (~$tamanoMb MB en dist\staging)." -ForegroundColor Green
}
else {
    Write-Host "`n=== Fase 1 omitida (-SoloCompilar) ===" -ForegroundColor DarkYellow
    if (-not (Test-Path -LiteralPath (Join-Path $carpetaStaging 'SManager.Gui.WinUI.exe'))) {
        throw "dist\staging no existe o está incompleto. Ejecuta sin -SoloCompilar."
    }
}

# --- Fase 2: compilar instalador Inno Setup ---
Write-Host "`n=== Fase 2: compilación Inno Setup ===" -ForegroundColor Yellow

New-Item -ItemType Directory -Path $carpetaSalidaInstalador -Force | Out-Null

$rutaIcono = Join-Path $raizRepo 'src\SManager.Gui.WinUI\Assets\AppIcon.ico'
$defineIcono = if (Test-Path -LiteralPath $rutaIcono) {
    "/DMySetupIcon=$rutaIcono"
}
else {
    Write-Warning "No se encontró AppIcon.ico; el instalador usará el icono por defecto de Inno."
    ''
}

$argumentosIscc = @(
    "/DMyAppVersion=$Version"
    "/DStagingDir=$carpetaStaging"
    "/DOutputDir=$carpetaSalidaInstalador"
)
if ($defineIcono) {
    $argumentosIscc += $defineIcono
}
$argumentosIscc += $scriptInno

& $rutaIscc @argumentosIscc
if ($LASTEXITCODE -ne 0) {
    throw "ISCC falló (código $LASTEXITCODE)."
}

$rutaSetup = Join-Path $carpetaSalidaInstalador "SManager2-Setup-$Version.exe"
if (-not (Test-Path -LiteralPath $rutaSetup)) {
    # Compatibilidad si el .iss no incluye versión en el nombre.
    $rutaSetup = Join-Path $carpetaSalidaInstalador 'SManager2-Setup.exe'
}

if (-not (Test-Path -LiteralPath $rutaSetup)) {
    throw 'No se generó el instalador en dist\.'
}

$tamanoSetupMb = [math]::Round((Get-Item -LiteralPath $rutaSetup).Length / 1MB, 1)
Write-Host "`nInstalador generado:" -ForegroundColor Green
Write-Host "  $rutaSetup (~$tamanoSetupMb MB)"
Write-Host @"

Entrega este único .exe al usuario final.
No necesita instalar .NET ni Windows App SDK: todo va embebido.
"@ -ForegroundColor DarkGray
