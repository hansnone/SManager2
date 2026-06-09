#Requires -Version 7.0
<#
.SYNOPSIS
    Regenera ICO/PNG de la app WinUI a partir de assets/icono.svg.
.DESCRIPTION
    Dependencias: rsvg-convert (librsvg) y Python con Pillow.
    Uso: ./tools/GenerarIconosApp.ps1
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$raizRepo = Split-Path -Parent $PSScriptRoot
$svg = Join-Path $raizRepo 'assets\icono.svg'

if (-not (Test-Path $svg)) {
    Write-Error "No se encuentra el SVG fuente: $svg"
}

if (-not (Get-Command rsvg-convert -ErrorAction SilentlyContinue)) {
    Write-Error 'Falta rsvg-convert (librsvg). Instálalo o añádelo al PATH.'
}

if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
    Write-Error 'Falta Python (con Pillow: pip install pillow).'
}

$scriptPython = @"
from PIL import Image
from pathlib import Path
import subprocess

raiz = Path(r"$($raizRepo -replace '\\', '\\\\')")
svg = raiz / "assets" / "icono.svg"
dest = raiz / "src" / "SManager.Gui.WinUI" / "Assets"
dest.mkdir(parents=True, exist_ok=True)

tamanos = {
    "Icono_16.png": 16,
    "Icono_32.png": 32,
    "Icono_48.png": 48,
    "Icono_256.png": 256,
    "Square44x44Logo.png": 44,
    "StoreLogo.png": 50,
    "Square150x150Logo.png": 150,
}

for nombre, tam in tamanos.items():
    subprocess.run(
        ["rsvg-convert", "-w", str(tam), "-h", str(tam), "-o", str(dest / nombre), str(svg)],
        check=True,
    )

icono = Image.open(dest / "Square150x150Logo.png").convert("RGBA")
wide = Image.new("RGBA", (310, 150), (255, 255, 255, 0))
wide.paste(icono, ((310 - icono.width) // 2, (150 - icono.height) // 2), icono)
wide.save(dest / "Wide310x150Logo.png")

splash_icono = Image.open(dest / "Icono_256.png").convert("RGBA")
splash = Image.new("RGBA", (620, 300), (255, 255, 255, 255))
splash.paste(
    splash_icono,
    ((620 - splash_icono.width) // 2, (300 - splash_icono.height) // 2),
    splash_icono,
)
splash.save(dest / "SplashScreen.png")

ico = [Image.open(dest / f"Icono_{s}.png") for s in (16, 32, 48, 256)]
ico[0].save(
    dest / "AppIcon.ico",
    format="ICO",
    sizes=[(s, s) for s in (16, 32, 48, 256)],
    append_images=ico[1:],
)
print(f"Iconos generados en {dest}")
"@

python -c $scriptPython
