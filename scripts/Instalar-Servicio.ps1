#Requires -Version 7.0
<#
.SYNOPSIS
    Instala SManager.Host como servicio Windows del usuario actual.
.DESCRIPTION
    El servicio debe ejecutarse con la cuenta del usuario para acceder a NAS,
    OneDrive y rutas UNC con las credenciales de sesión.
.EXAMPLE
    .\Instalar-Servicio.ps1 -RutaHost "C:\Apps\SManager2\SManager.Host.exe"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$RutaHost
)

$ErrorActionPreference = 'Stop'
$nombreServicio = 'SManager2'

if (-not (Test-Path -LiteralPath $RutaHost)) {
    throw "No existe el ejecutable: $RutaHost"
}

$rutaHost = (Resolve-Path -LiteralPath $RutaHost).Path
$binarioSc = Join-Path $env:WINDIR 'System32\sc.exe'

# Crear o reemplazar el servicio apuntando al modo supervisor.
& $binarioSc stop $nombreServicio 2>$null
& $binarioSc delete $nombreServicio 2>$null
Start-Sleep -Seconds 2

& $binarioSc create $nombreServicio binPath= "`"$rutaHost`" --servicio" start= auto DisplayName= "SManager 2.0 - Gestor de sincronización"
& $binarioSc description $nombreServicio "Sincronización unidireccional origen→destino. Datos en %LOCALAPPDATA%\SManager2"

Write-Host "Servicio '$nombreServicio' registrado." -ForegroundColor Green
Write-Host @"

IMPORTANTE — configurar cuenta de usuario:
  1. services.msc → SManager2 → Propiedades → Iniciar sesión
  2. Elige 'Esta cuenta' y usa tu usuario de Windows (necesario para NAS/OneDrive)
  3. Inicia el servicio manualmente o reinicia el equipo

Los demonios por perfil se arrancan con: smanager start -Perfil <nombre> -ConfigPath <json>
"@ -ForegroundColor Yellow
