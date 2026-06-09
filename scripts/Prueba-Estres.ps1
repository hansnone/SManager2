#Requires -Version 7.0
<#
.SYNOPSIS
    Prueba de estrés avanzada para SManager 2.0 (Escritorio\A → Escritorio\B).
.DESCRIPTION
    Genera escenarios pesados y complejos: miles de archivos, árboles anchos/profundos,
    archivos enormes, ráfagas paralelas, escritura lenta, renombres en caliente, caos
    concurrente y validación de integridad (tamaño + hash SHA-256 en muestra).

    Perfil IPC por defecto: Estres (cámbialo en la GUI para ver actividad en vivo).
.PARAMETER Intensidad
    Normal | Alto | Extremo — ajusta volúmenes si no pasas parámetros individuales.
.PARAMETER Escenario
    Todos, Pequenos, Ancho, Profundo, MezclaTamanos, Grandes, ColaSaturada, Rafaga,
    Paralelo, EscrituraLenta, Renombres, Modificaciones, AppendContinuo, NombresComplejos,
    Exclusiones, CaosCaliente
.EXAMPLE
    .\scripts\Prueba-Estres.ps1 -Intensidad Extremo -ConfirmarTodo
.EXAMPLE
    .\scripts\Prueba-Estres.ps1 -Escenario CaosCaliente -ConfirmarTodo
#>
[CmdletBinding()]
param(
    [string]$RutaOrigen = (Join-Path ([Environment]::GetFolderPath('Desktop')) 'A'),
    [string]$RutaDestino = (Join-Path ([Environment]::GetFolderPath('Desktop')) 'B'),
    [string]$Perfil = 'Estres',
    [ValidateSet('Normal', 'Alto', 'Extremo')]
    [string]$Intensidad = 'Alto',
    [ValidateSet(
        'Todos', 'Pequenos', 'Ancho', 'Profundo', 'MezclaTamanos', 'Grandes',
        'ColaSaturada', 'Rafaga', 'Paralelo', 'EscrituraLenta', 'Renombres',
        'Modificaciones', 'AppendContinuo', 'NombresComplejos', 'Exclusiones', 'CaosCaliente'
    )]
    [string]$Escenario = 'Todos',
    [int]$CantidadArchivosPequenos = 5000,
    [int]$TamanoArchivoPequenoBytes = 1024,
    [int]$CantidadArchivosGrandes = 5,
    [int]$TamanoArchivoGrandeMB = 80,
    [int]$ProfundidadDirectorios = 10,
    [int]$AnchuraCarpetas = 80,
    [int]$ArchivosPorCarpetaAncha = 25,
    [int]$ArchivosPorRafaga = 400,
    [int]$Rafagas = 6,
    [int]$ArchivosColaSaturada = 2500,
    [int]$ArchivosCreacionParalela = 1500,
    [int]$NumHilosParalelos = 12,
    [int]$ArchivosRenombrar = 200,
    [int]$ArchivosModificar = 300,
    [int]$SegundosCaosCaliente = 45,
    [int]$MuestrasHashIntegridad = 150,
    [int]$TimeoutEsperaSegundos = 1800,
    [switch]$LimpiarOrigen,
    [switch]$LimpiarDestino,
    [switch]$SoloGenerar,
    [switch]$NoIniciarDemonio,
    [switch]$ConfirmarTodo,
    [string]$RutaSmanager
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- Aplicar preset de intensidad (solo parámetros no fijados explícitamente) ---
function Set-ValorSiNoDefinido {
    param([string]$Nombre, $Valor)
    if (-not $PSBoundParameters.ContainsKey($Nombre)) {
        Set-Variable -Name $Nombre -Value $Valor -Scope Script
    }
}

switch ($Intensidad) {
    'Normal' {
        Set-ValorSiNoDefinido CantidadArchivosPequenos 2000
        Set-ValorSiNoDefinido TamanoArchivoGrandeMB 40
        Set-ValorSiNoDefinido CantidadArchivosGrandes 3
        Set-ValorSiNoDefinido ProfundidadDirectorios 8
        Set-ValorSiNoDefinido AnchuraCarpetas 40
        Set-ValorSiNoDefinido ArchivosColaSaturada 800
        Set-ValorSiNoDefinido ArchivosCreacionParalela 600
        Set-ValorSiNoDefinido NumHilosParalelos 6
        Set-ValorSiNoDefinido TimeoutEsperaSegundos 900
    }
    'Extremo' {
        Set-ValorSiNoDefinido CantidadArchivosPequenos 15000
        Set-ValorSiNoDefinido TamanoArchivoGrandeMB 150
        Set-ValorSiNoDefinido CantidadArchivosGrandes 8
        Set-ValorSiNoDefinido ProfundidadDirectorios 14
        Set-ValorSiNoDefinido AnchuraCarpetas 150
        Set-ValorSiNoDefinido ArchivosPorCarpetaAncha 40
        Set-ValorSiNoDefinido ArchivosPorRafaga 800
        Set-ValorSiNoDefinido Rafagas 10
        Set-ValorSiNoDefinido ArchivosColaSaturada 6000
        Set-ValorSiNoDefinido ArchivosCreacionParalela 4000
        Set-ValorSiNoDefinido NumHilosParalelos 16
        Set-ValorSiNoDefinido ArchivosRenombrar 500
        Set-ValorSiNoDefinido ArchivosModificar 800
        Set-ValorSiNoDefinido SegundosCaosCaliente 90
        Set-ValorSiNoDefinido MuestrasHashIntegridad 300
        Set-ValorSiNoDefinido TimeoutEsperaSegundos 3600
    }
}

# --- Config del demonio durante estrés (más copiadores, estabilidad corta) ---
$Script:FiltroInclusion = '*'
$Script:FiltroExclusion = '~$*;*.tmp;*.partial;*.lnk'
$Script:IntervaloPollingSegundos = 15
$Script:SegundosEstabilidad = 1
$Script:NumCopiadores = [math]::Min(12, [math]::Max(4, [int][math]::Ceiling($NumHilosParalelos / 2)))
$Script:ColaMaximaObservada = 0

function Write-Fase {
    param([string]$Mensaje)
    Write-Host "`n=== $Mensaje ===" -ForegroundColor Cyan
}

function Write-Metrica {
    param([string]$Etiqueta, [object]$Valor)
    Write-Host ("  {0,-28} {1}" -f $Etiqueta, $Valor) -ForegroundColor DarkGray
}

function Confirmar-SiNecesario {
    param([string]$Mensaje)
    if ($ConfirmarTodo) { return $true }
    return (Read-Host "$Mensaje [s/N]") -match '^[sSyY]'
}

function Get-RutaSmanager {
    if ($RutaSmanager -and (Test-Path -LiteralPath $RutaSmanager)) {
        return (Resolve-Path -LiteralPath $RutaSmanager).Path
    }
    $raizRepo = Split-Path -Parent $PSScriptRoot
    @(
        (Get-Command 'smanager.exe' -ErrorAction SilentlyContinue)?.Source
        "$env:LOCALAPPDATA\Programs\SManager2\herramientas\smanager.exe"
        (Join-Path $raizRepo 'src\SManager.Cli\bin\Release\net8.0\win-x64\smanager.exe')
        (Join-Path $raizRepo 'src\SManager.Cli\bin\Debug\net8.0\smanager.exe')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
}

function Test-ArchivoExcluido {
    param([string]$NombreArchivo)
    foreach ($patron in ($Script:FiltroExclusion -split ';')) {
        $patron = $patron.Trim()
        if ([string]::IsNullOrWhiteSpace($patron)) { continue }
        if ($patron.StartsWith('*.')) {
            if ($NombreArchivo.EndsWith($patron.Substring(1), [StringComparison]::OrdinalIgnoreCase)) { return $true }
        }
        elseif ($patron.EndsWith('*')) {
            if ($NombreArchivo.StartsWith($patron.TrimEnd('*'), [StringComparison]::OrdinalIgnoreCase)) { return $true }
        }
        elseif ($NombreArchivo -like $patron) { return $true }
    }
    return $false
}

function Get-ArchivosOrigenEsperados {
    param([string]$Raiz)
    Get-ChildItem -LiteralPath $Raiz -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { -not (Test-ArchivoExcluido -NombreArchivo $_.Name) }
}

function Get-RutaConfiguracionPerfil {
    # Misma ubicación por defecto que ResolvedorRutasConfiguracion / ServicioConfiguracionGui.
    Join-Path $env:LOCALAPPDATA "SManager2\Perfiles configuracion\$Perfil\configuracion.json"
}

function Get-RutaConfiguracionResuelta {
    $prefs = Join-Path $env:LOCALAPPDATA "SManager2\Perfiles\$Perfil\preferencias.json"
    if (Test-Path -LiteralPath $prefs) {
        try {
            $json = Get-Content -LiteralPath $prefs -Raw -Encoding UTF8 | ConvertFrom-Json
            $personalizada = $json.ruta_configuracion_personalizada
            if ($personalizada -and (Test-Path -LiteralPath $personalizada)) {
                return (Resolve-Path -LiteralPath $personalizada).Path
            }
        }
        catch { }
    }
    return Get-RutaConfiguracionPerfil
}

function New-ConfiguracionEstres {
    $config = [ordered]@{
        version                       = 1
        intervalo_polling_segundos    = $Script:IntervaloPollingSegundos
        segundos_estabilidad_archivo  = $Script:SegundosEstabilidad
        num_copiadores_paralelos      = $Script:NumCopiadores
        num_hidratadores_paralelos    = 3
        timeout_hidratacion_segundos  = 300
        intervalo_publicacion_estado_ms = 500
        pares                         = @(
            [ordered]@{
                id_par           = 'estres-desktop-ab'
                nombre           = 'Escritorio A → B (estrés)'
                habilitado       = $true
                pausado          = $false
                ruta_origen      = (Resolve-Path -LiteralPath $RutaOrigen).Path
                ruta_destino     = (Resolve-Path -LiteralPath $RutaDestino).Path
                filtro_inclusion = $Script:FiltroInclusion
                filtro_exclusion = $Script:FiltroExclusion
                total_copiados   = 0
                total_errores    = 0
            }
        )
    }
    $rutaConfig = (Get-RutaConfiguracionResuelta)
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $rutaConfig) -Force
    $config | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $rutaConfig -Encoding UTF8
    return $rutaConfig
}

function Get-RutaEstadoPerfil {
    Join-Path $env:LOCALAPPDATA "SManager2\Perfiles\$Perfil\estado.json"
}

function Get-EstadoSmanager {
    $ruta = Get-RutaEstadoPerfil
    if (-not (Test-Path -LiteralPath $ruta)) { return $null }
    try { return Get-Content -LiteralPath $ruta -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { return $null }
}

function Show-EstadoSmanager {
    $estado = Get-EstadoSmanager
    if (-not $estado) {
        Write-Metrica 'Estado IPC' '(sin telemetría)'
        return
    }
    if ([int]$estado.cola_copia_pendiente -gt $Script:ColaMaximaObservada) {
        $Script:ColaMaximaObservada = [int]$estado.cola_copia_pendiente
    }
    Write-Metrica 'Cola copia' $estado.cola_copia_pendiente
    Write-Metrica 'Pendientes únicos' $estado.archivos_unicos_pendientes
    Write-Metrica 'Copiados sesión' $estado.totales.copiados
    Write-Metrica 'Errores sesión' $estado.totales.errores
    Write-Metrica 'Bytes escritos' $estado.totales.bytes_escritos
    Write-Metrica 'Cola máxima (pico)' $Script:ColaMaximaObservada
    if ($estado.copias_en_curso -and $estado.copias_en_curso.Count -gt 0) {
        $c = $estado.copias_en_curso[0]
        Write-Metrica 'Copia en curso' "$($c.archivo) $($c.porcentaje)%"
    }
    if ($estado.recursos) {
        Write-Metrica 'RAM demonio (MB)' ([math]::Round($estado.recursos.memoria_trabajo_bytes / 1MB, 1))
        Write-Metrica 'CPU demonio (%)' $estado.recursos.cpu_porcentaje
    }
}

function Wait-SincronizacionCompleta {
    param(
        [int]$ArchivosNuevosEnFase,
        [int]$ConteoDestinoInicial,
        [int]$TimeoutSegundos = 1800,
        [switch]$SoloColaVacia
    )

    $archivosDestinoObjetivo = $ConteoDestinoInicial + $ArchivosNuevosEnFase
    $limite = [datetime]::UtcNow.AddSeconds($TimeoutSegundos)
    $ultimoCopiado = -1
    $segundosSinCambio = 0

    if ($SoloColaVacia -or $ArchivosNuevosEnFase -le 0) {
        Write-Fase 'Esperando cola vacía (fase caótica / sin archivos netos nuevos)'
        $archivosDestinoObjetivo = -1
    }
    else {
        Write-Fase "Esperando +$ArchivosNuevosEnFase archivos nuevos en B (timeout ${TimeoutSegundos}s)"
    }

    while ([datetime]::UtcNow -lt $limite) {
        $enDestino = @(Get-ArchivosOrigenEsperados -Raiz $RutaDestino).Count
        $estado = Get-EstadoSmanager
        $cola = if ($estado) { [int]$estado.cola_copia_pendiente } else { -1 }
        $pendientes = if ($estado) { [int]$estado.archivos_unicos_pendientes } else { -1 }
        $copiados = if ($estado) { [int]$estado.totales.copiados } else { 0 }
        $errores = if ($estado) { [int]$estado.totales.errores } else { 0 }

        if ($cola -gt $Script:ColaMaximaObservada) { $Script:ColaMaximaObservada = $cola }

        if ($archivosDestinoObjetivo -ge 0) {
            Write-Host ("  B:{0}/{1} cola={2} pend={3} cop={4} err={5}" -f `
                $enDestino, $archivosDestinoObjetivo, $cola, $pendientes, $copiados, $errores) -ForegroundColor DarkYellow
            $destinoOk = $enDestino -ge $archivosDestinoObjetivo
        }
        else {
            Write-Host ("  B:{0} cola={1} pend={2} cop={3} err={4}" -f `
                $enDestino, $cola, $pendientes, $copiados, $errores) -ForegroundColor DarkYellow
            $destinoOk = $true
        }

        if ($destinoOk -and $cola -eq 0 -and $pendientes -eq 0) {
            Write-Host '  Sincronización completada.' -ForegroundColor Green
            return $true
        }

        if ($copiados -eq $ultimoCopiado) { $segundosSinCambio += 2 }
        else { $segundosSinCambio = 0; $ultimoCopiado = $copiados }

        if ($segundosSinCambio -ge 45 -and $cola -eq 0 -and $pendientes -eq 0 -and -not $destinoOk) {
            return $false
        }
        Start-Sleep -Seconds 2
    }
    Write-Host '  Timeout.' -ForegroundColor Red
    return $false
}

function Get-HashArchivo {
    param([string]$Ruta)
    (Get-FileHash -LiteralPath $Ruta -Algorithm SHA256).Hash
}

function Test-IntegridadOrigenDestino {
    param([int]$MuestrasHash = 0)

    $origen = @(Get-ArchivosOrigenEsperados -Raiz $RutaOrigen)
    $faltantes = [System.Collections.Generic.List[string]]::new()
    $tamanoIncorrecto = [System.Collections.Generic.List[string]]::new()
    $hashIncorrecto = [System.Collections.Generic.List[string]]::new()

    foreach ($archivo in $origen) {
        $relativo = $archivo.FullName.Substring($RutaOrigen.Length).TrimStart('\')
        $destino = Join-Path $RutaDestino $relativo
        if (-not (Test-Path -LiteralPath $destino)) {
            $faltantes.Add($relativo)
            continue
        }
        $infoDestino = Get-Item -LiteralPath $destino
        if ($infoDestino.Length -ne $archivo.Length) {
            $tamanoIncorrecto.Add("$relativo ($($archivo.Length) vs $($infoDestino.Length))")
        }
    }

    if ($MuestrasHash -gt 0 -and $origen.Count -gt 0) {
        $muestra = $origen | Get-Random -Count ([math]::Min($MuestrasHash, $origen.Count))
        foreach ($archivo in $muestra) {
            if ($archivo.Length -gt 50MB) { continue }
            $relativo = $archivo.FullName.Substring($RutaOrigen.Length).TrimStart('\')
            $destino = Join-Path $RutaDestino $relativo
            if (-not (Test-Path -LiteralPath $destino)) { continue }
            if ((Get-HashArchivo $archivo.FullName) -ne (Get-HashArchivo $destino)) {
                $hashIncorrecto.Add($relativo)
            }
        }
    }

    [pscustomobject]@{
        Ok               = ($faltantes.Count -eq 0) -and ($tamanoIncorrecto.Count -eq 0) -and ($hashIncorrecto.Count -eq 0)
        TotalOrigen      = $origen.Count
        Faltantes        = $faltantes
        TamanoIncorrecto = $tamanoIncorrecto
        HashIncorrecto   = $hashIncorrecto
    }
}

function Get-SumaBytes {
    param([string]$Raiz)
    $medida = Get-ArchivosOrigenEsperados -Raiz $Raiz | Measure-Object -Property Length -Sum
    if ($null -eq $medida -or $null -eq $medida.Sum) { return 0L }
    return [long]$medida.Sum
}

function New-ContenidoAleatorio {
    param([int]$Bytes)
    $buffer = New-Object byte[] $Bytes
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($buffer)
    return $buffer
}

function New-ArchivoBinario {
    param([string]$Ruta, [int]$Bytes, [byte[]]$Plantilla)
    $dir = Split-Path -Parent $Ruta
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    if ($Plantilla -and $Plantilla.Length -eq $Bytes) {
        [System.IO.File]::WriteAllBytes($Ruta, $Plantilla)
        return
    }
    [System.IO.File]::WriteAllBytes($Ruta, (New-ContenidoAleatorio -Bytes $Bytes))
}

function Invoke-FasePequenos {
    Write-Fase "Pequeños masivos ($CantidadArchivosPequenos × $TamanoArchivoPequenoBytes B, paralelo)"
    $destino = Join-Path $RutaOrigen 'pequenos'
    New-Item -ItemType Directory -Path $destino -Force | Out-Null
    $plantilla = New-ContenidoAleatorio -Bytes $TamanoArchivoPequenoBytes

    0..($CantidadArchivosPequenos - 1) | ForEach-Object -Parallel {
        $i = $_
        $dir = $using:destino
        $tam = $using:TamanoArchivoPequenoBytes
        $buf = $using:plantilla
        $ruta = Join-Path $dir ("f_{0:D6}.bin" -f $i)
        if ($buf.Length -eq $tam) { [System.IO.File]::WriteAllBytes($ruta, $buf) }
        else { [System.IO.File]::WriteAllBytes($ruta, [byte[]]::new($tam)) }
        if ($i -gt 0 -and ($i % 1000) -eq 0) { Write-Host "  ... $i" -ForegroundColor DarkGray }
    } -ThrottleLimit $NumHilosParalelos

    Write-Host "  $CantidadArchivosPequenos archivos en pequenos\" -ForegroundColor Green
}

function Invoke-FaseAncho {
    Write-Fase "Árbol ancho ($AnchuraCarpetas carpetas × $ArchivosPorCarpetaAncha archivos)"
    $raiz = Join-Path $RutaOrigen 'ancho'
    New-Item -ItemType Directory -Path $raiz -Force | Out-Null

    0..($AnchuraCarpetas - 1) | ForEach-Object -Parallel {
        $c = $_
        $base = $using:raiz
        $porCarpeta = $using:ArchivosPorCarpetaAncha
        $carpeta = Join-Path $base ("carpeta_{0:D4}" -f $c)
        [void][System.IO.Directory]::CreateDirectory($carpeta)
        for ($f = 0; $f -lt $porCarpeta; $f++) {
            $ruta = Join-Path $carpeta ("a_{0:D3}.dat" -f $f)
            [System.IO.File]::WriteAllBytes($ruta, [byte[]]::new(768))
        }
    } -ThrottleLimit $NumHilosParalelos
    Write-Host '  Árbol ancho creado.' -ForegroundColor Green
}

function Invoke-FaseProfundo {
    Write-Fase "Árbol profundo ($ProfundidadDirectorios niveles + ramas)"
    $actual = Join-Path $RutaOrigen 'profundo'
    New-Item -ItemType Directory -Path $actual -Force | Out-Null

    for ($n = 0; $n -lt $ProfundidadDirectorios; $n++) {
        $actual = Join-Path $actual ("nivel_{0:D2}" -f $n)
        New-Item -ItemType Directory -Path $actual -Force | Out-Null
        New-ArchivoBinario -Ruta (Join-Path $actual 'dato.bin') -Bytes (4096 * ($n + 1))
        for ($s = 0; $s -lt 3; $s++) {
            $sub = Join-Path $actual ("sub_{0}" -f $s)
            New-Item -ItemType Directory -Path $sub -Force | Out-Null
            New-ArchivoBinario -Ruta (Join-Path $sub 'hoja.bin') -Bytes (512 * ($s + 1))
        }
    }

    0..19 | ForEach-Object {
        $rama = Join-Path $RutaOrigen "profundo\rama_extra_$($_)"
        New-Item -ItemType Directory -Path $rama -Force | Out-Null
        0..24 | ForEach-Object {
            New-ArchivoBinario -Ruta (Join-Path $rama "x_$($_).bin") -Bytes (Get-Random -Minimum 256 -Maximum 8192)
        }
    }
    Write-Host '  Árbol profundo creado.' -ForegroundColor Green
}

function Invoke-FaseMezclaTamanos {
    Write-Fase 'Mezcla de tamaños en la misma carpeta (1 B … 15 MB)'
    $destino = Join-Path $RutaOrigen 'mezcla'
    New-Item -ItemType Directory -Path $destino -Force | Out-Null

    @(1, 10, 100, 1024, 65536, 512KB, 2MB, 8MB, 15MB) | ForEach-Object {
        $tam = [int]$_
        New-ArchivoBinario -Ruta (Join-Path $destino "tam_$tam.bin") -Bytes $tam
    }

    0..199 | ForEach-Object -Parallel {
        $i = $_
        $dir = $using:destino
        $tam = Get-Random -Minimum 1 -Maximum 65536
        [System.IO.File]::WriteAllBytes((Join-Path $dir ("mix_{0:D4}.bin" -f $i)), [byte[]]::new($tam))
    } -ThrottleLimit $NumHilosParalelos
    Write-Host '  Mezcla creada.' -ForegroundColor Green
}

function Invoke-FaseGrandes {
    Write-Fase "Archivos grandes ($CantidadArchivosGrandes × ~$TamanoArchivoGrandeMB MB)"
    $destino = Join-Path $RutaOrigen 'grandes'
    New-Item -ItemType Directory -Path $destino -Force | Out-Null
    $bytesPorArchivo = [long]$TamanoArchivoGrandeMB * 1MB
    $bloque = New-ContenidoAleatorio -Bytes 1MB

    for ($i = 0; $i -lt $CantidadArchivosGrandes; $i++) {
        $ruta = Join-Path $destino ("grande_{0:D2}.bin" -f $i)
        $stream = [System.IO.File]::Open($ruta, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
        try {
            $escritos = 0L
            while ($escritos -lt $bytesPorArchivo) {
                $stream.Write($bloque, 0, $bloque.Length)
                $escritos += $bloque.Length
            }
        }
        finally { $stream.Dispose() }
        Write-Host ("  grande_{0:D2}.bin ({1} MB)" -f $i, $TamanoArchivoGrandeMB) -ForegroundColor DarkGray
    }
}

function Invoke-FaseColaSaturada {
    Write-Fase "Saturación de cola ($ArchivosColaSaturada archivos en ráfaga única)"
    $destino = Join-Path $RutaOrigen 'cola_saturada'
    New-Item -ItemType Directory -Path $destino -Force | Out-Null

    0..($ArchivosColaSaturada - 1) | ForEach-Object -Parallel {
        $i = $_
        $dir = $using:destino
        $ruta = Join-Path $dir ("burst_{0:D6}.bin" -f $i)
        [System.IO.File]::WriteAllBytes($ruta, [byte[]]::new(256))
    } -ThrottleLimit $NumHilosParalelos
    Write-Host '  Ráfaga de cola enviada.' -ForegroundColor Green
}

function Invoke-FaseRafaga {
    Write-Fase "Ráfagas escalonadas ($Rafagas × $ArchivosPorRafaga)"
    $destino = Join-Path $RutaOrigen 'rafaga'
    New-Item -ItemType Directory -Path $destino -Force | Out-Null

    for ($r = 0; $r -lt $Rafagas; $r++) {
        Write-Host "  Ráfaga $($r + 1)/$Rafagas..." -ForegroundColor DarkGray
        0..($ArchivosPorRafaga - 1) | ForEach-Object -Parallel {
            $f = $_
            $dir = $using:destino
            $numR = $using:r
            $nombre = "r{0:D2}_f{1:D5}.bin" -f $numR, $f
            $bytes = Get-Random -Minimum 64 -Maximum 16384
            [System.IO.File]::WriteAllBytes((Join-Path $dir $nombre), [byte[]]::new($bytes))
        } -ThrottleLimit $NumHilosParalelos
        Show-EstadoSmanager
        Start-Sleep -Milliseconds 500
    }
}

function Invoke-FaseParalelo {
    Write-Fase "Creación paralela concurrente ($ArchivosCreacionParalela archivos, $NumHilosParalelos hilos)"
    $destino = Join-Path $RutaOrigen 'paralelo'
    New-Item -ItemType Directory -Path $destino -Force | Out-Null

    0..($ArchivosCreacionParalela - 1) | ForEach-Object -Parallel {
        $i = $_
        $dir = $using:destino
        $sub = Join-Path $dir ("h_{0:D2}" -f ($i % 32))
        [void][System.IO.Directory]::CreateDirectory($sub)
        $tam = Get-Random -Minimum 128 -Maximum 32768
        [System.IO.File]::WriteAllBytes((Join-Path $sub ("p_{0:D6}.bin" -f $i)), [byte[]]::new($tam))
    } -ThrottleLimit $NumHilosParalelos
    Write-Host '  Creación paralela completada.' -ForegroundColor Green
}

function Invoke-FaseEscrituraLenta {
    Write-Fase 'Escritura lenta (archivos que crecen durante la estabilidad)'
    $destino = Join-Path $RutaOrigen 'escritura_lenta'
    New-Item -ItemType Directory -Path $destino -Force | Out-Null

    1..8 | ForEach-Object -Parallel {
        $i = $_
        $dir = $using:destino
        $ruta = Join-Path $dir ("lento_{0}.bin" -f $i)
        $stream = [System.IO.File]::Open($ruta, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
        try {
            for ($b = 0; $b -lt 40; $b++) {
                $stream.Write([byte[]]::new(8192), 0, 8192)
                $stream.Flush()
                Start-Sleep -Milliseconds (Get-Random -Minimum 80 -Maximum 250)
            }
        }
        finally { $stream.Dispose() }
    } -ThrottleLimit 4
    Write-Host '  Escrituras lentas finalizadas.' -ForegroundColor Green
}

function Invoke-FaseRenombres {
    Write-Fase "Renombres en caliente ($ArchivosRenombrar archivos)"
    $destino = Join-Path $RutaOrigen 'renombres'
    New-Item -ItemType Directory -Path $destino -Force | Out-Null

    $creados = [System.Collections.Generic.List[string]]::new()
    for ($i = 0; $i -lt $ArchivosRenombrar; $i++) {
        $ruta = Join-Path $destino ("orig_{0:D5}.txt" -f $i)
        "contenido-$i" | Set-Content -LiteralPath $ruta -Encoding UTF8 -NoNewline
        $creados.Add($ruta)
    }

    Start-Sleep -Seconds 2
    $mitad = [math]::Min($ArchivosRenombrar, [math]::Floor($creados.Count / 2))
    for ($i = 0; $i -lt $mitad; $i++) {
        $viejo = $creados[$i]
        $nuevo = Join-Path $destino ("renombrado_{0:D5}.txt" -f $i)
        Move-Item -LiteralPath $viejo -Destination $nuevo -Force
    }

    # Mover a subcarpeta distinta (cambia ruta relativa)
    $sub = Join-Path $destino 'movidos'
    New-Item -ItemType Directory -Path $sub -Force | Out-Null
    for ($i = $mitad; $i -lt [math]::Min($creados.Count, $mitad + 50); $i++) {
        if (Test-Path -LiteralPath $creados[$i]) {
            Move-Item -LiteralPath $creados[$i] -Destination (Join-Path $sub (Split-Path -Leaf $creados[$i])) -Force
        }
    }
    Write-Host "  Renombrados/movidos $($mitad + 50) archivos." -ForegroundColor Green
}

function Invoke-FaseModificaciones {
    Write-Fase "Modificaciones masivas ($ArchivosModificar archivos)"
    $carpetas = @(
        (Join-Path $RutaOrigen 'pequenos')
        (Join-Path $RutaOrigen 'mezcla')
        (Join-Path $RutaOrigen 'ancho')
    ) | Where-Object { Test-Path -LiteralPath $_ }

    if ($carpetas.Count -eq 0) {
        Write-Host '  (sin carpetas previas; creando muestra)' -ForegroundColor Yellow
        Invoke-FasePequenos
        $carpetas = @((Join-Path $RutaOrigen 'pequenos'))
    }

    $todos = foreach ($c in $carpetas) {
        Get-ChildItem -LiteralPath $c -Recurse -File -ErrorAction SilentlyContinue
    }
    $muestra = @($todos | Select-Object -First $ArchivosModificar)
    foreach ($archivo in $muestra) {
        $nuevoTam = [int]$archivo.Length + (Get-Random -Minimum 32 -Maximum 4096)
        New-ArchivoBinario -Ruta $archivo.FullName -Bytes $nuevoTam
    }
    Write-Host "  Modificados $($muestra.Count) archivos." -ForegroundColor Green
}

function Invoke-FaseAppendContinuo {
    Write-Fase 'Append continuo (archivos que crecen tras copia inicial)'
    $destino = Join-Path $RutaOrigen 'append'
    New-Item -ItemType Directory -Path $destino -Force | Out-Null

    $rutas = 0..29 | ForEach-Object {
        $r = Join-Path $destino ("creciente_{0:D2}.log" -f $_)
        New-ArchivoBinario -Ruta $r -Bytes 4096
        $r
    }

    foreach ($ronda in 1..5) {
        foreach ($ruta in $rutas) {
            $stream = [System.IO.File]::Open($ruta, [System.IO.FileMode]::Append, [System.IO.FileAccess]::Write)
            try { $stream.Write([byte[]]::new(2048), 0, 2048) }
            finally { $stream.Dispose() }
        }
        Start-Sleep -Milliseconds 400
    }
    Write-Host '  30 archivos × 5 appends.' -ForegroundColor Green
}

function Invoke-FaseNombresComplejos {
    Write-Fase 'Nombres complejos (unicode, espacios, rutas largas)'
    $destino = Join-Path $RutaOrigen 'nombres_complejos'
    New-Item -ItemType Directory -Path $destino -Force | Out-Null

    $casos = @(
        'Informe año 2026 — revisión (final).txt'
        '日本語ファイル.dat'
        'archivo  con   espacios.bin'
        'comercial#precio$100.txt'
        'ñandú_español.txt'
    )
    foreach ($nombre in $casos) {
        "test" | Set-Content -LiteralPath (Join-Path $destino $nombre) -Encoding UTF8
    }

    $segmentoLargo = ('L' * 80)
    $rutaLarga = Join-Path $destino ($segmentoLargo + '\sub_' + ('M' * 60) + '\archivo_largo.bin')
    New-Item -ItemType Directory -Path (Split-Path -Parent $rutaLarga) -Force | Out-Null
    New-ArchivoBinario -Ruta $rutaLarga -Bytes 2048

    # Mismo nombre de archivo en ramas distintas
    0..9 | ForEach-Object {
        $c = Join-Path $destino "rama_$_"
        New-Item -ItemType Directory -Path $c -Force | Out-Null
        "rama $_" | Set-Content -LiteralPath (Join-Path $c 'mismo_nombre.txt') -Encoding UTF8
    }
    Write-Host '  Nombres complejos creados.' -ForegroundColor Green
}

function Invoke-FaseExclusiones {
    Write-Fase 'Filtros de exclusión (no deben copiarse)'
    $destino = Join-Path $RutaOrigen 'exclusiones'
    New-Item -ItemType Directory -Path $destino -Force | Out-Null

    @{
        'borrador.tmp'       = 'tmp'
        'descarga.partial'   = 'partial'
        '~$documento.xlsx'   = 'office temp'
        'valido.txt'         = 'ok'
        'valido2.bin'        = 'ok'
    }.GetEnumerator() | ForEach-Object {
        $_.Value | Set-Content -LiteralPath (Join-Path $destino $_.Key) -Encoding UTF8
    }
    Write-Host '  Exclusiones creadas (solo valido.* debe llegar a B).' -ForegroundColor Green
}

function Invoke-FaseCaosCaliente {
    Write-Fase "Caos caliente ($SegundosCaosCaliente s de actividad concurrente)"
    $caosDir = Join-Path $RutaOrigen 'caos'
    New-Item -ItemType Directory -Path $caosDir -Force | Out-Null
    $finUtc = [datetime]::UtcNow.AddSeconds($SegundosCaosCaliente)
    $finTicks = $finUtc.Ticks
    $carpetaPequenos = Join-Path $RutaOrigen 'pequenos'

    # Tres procesos en paralelo: alta de archivos, modificaciones y renombres simultáneos.
    $jobAlta = Start-Job -ScriptBlock {
        param($Dir, $FinTicks)
        $n = 0
        while ([datetime]::UtcNow.Ticks -lt $FinTicks) {
            $n++
            [System.IO.File]::WriteAllBytes(
                (Join-Path $Dir "caos_n_$n.bin"),
                [byte[]]::new((Get-Random -Minimum 64 -Maximum 8192)))
        }
    } -ArgumentList $caosDir, $finTicks

    $jobModifica = Start-Job -ScriptBlock {
        param($Pequenos, $FinTicks)
        if (-not (Test-Path -LiteralPath $Pequenos)) { return }
        $archivos = @(Get-ChildItem -LiteralPath $Pequenos -File -ErrorAction SilentlyContinue | Select-Object -First 400)
        while ([datetime]::UtcNow.Ticks -lt $FinTicks) {
            foreach ($a in $archivos) {
                try {
                    [System.IO.File]::WriteAllBytes($a.FullName, [byte[]]::new((Get-Random -Minimum 256 -Maximum 4096)))
                }
                catch { }
            }
            Start-Sleep -Milliseconds 200
        }
    } -ArgumentList $carpetaPequenos, $finTicks

    $jobRenombra = Start-Job -ScriptBlock {
        param($Dir, $FinTicks)
        $i = 0
        while ([datetime]::UtcNow.Ticks -lt $FinTicks) {
            $i++
            $orig = Join-Path $Dir "r_$i.bin"
            $dest = Join-Path $Dir "r_${i}_movido.bin"
            [System.IO.File]::WriteAllBytes($orig, [byte[]]::new(256))
            Start-Sleep -Milliseconds 40
            if (Test-Path -LiteralPath $orig) {
                try { Move-Item -LiteralPath $orig -Destination $dest -Force } catch { }
            }
        }
    } -ArgumentList $caosDir, $finTicks

    while ([datetime]::UtcNow -lt $finUtc) {
        Show-EstadoSmanager
        Start-Sleep -Seconds 3
    }

    Wait-Job -Job $jobAlta, $jobModifica, $jobRenombra | Out-Null
    Remove-Job -Job $jobAlta, $jobModifica, $jobRenombra -Force
    Write-Host '  Caos terminado (altas + modificaciones + renombres concurrentes).' -ForegroundColor Green
}

function Clear-CarpetaPrueba {
    param([string]$Ruta, [string]$Etiqueta)
    if (-not (Test-Path -LiteralPath $Ruta)) { return }
    Write-Host "  Vaciando $Etiqueta : $Ruta" -ForegroundColor Yellow
    Get-ChildItem -LiteralPath $Ruta -Force | Remove-Item -Recurse -Force
}

function Start-DemonioEstres {
    param([string]$RutaConfig)
    $cli = $Script:RutaSmanagerResuelta
    Write-Fase "Demonio perfil '$Perfil'"
    & $cli status -perfil $Perfil 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host '  Demonio activo; recargando config...' -ForegroundColor Green
        & $cli reload -perfil $Perfil | Out-Null
        return
    }
    & $cli start -perfil $Perfil
    if ($LASTEXITCODE -ne 0) { throw "smanager start falló ($LASTEXITCODE)" }
}

# =============================================================================
$fasesCaoticas = @('Modificaciones', 'Renombres', 'AppendContinuo', 'CaosCaliente', 'EscrituraLenta')

$inicioTotal = [datetime]::UtcNow
$resultadosFases = [System.Collections.Generic.List[object]]::new()

Write-Host @"

SManager 2.0 — estrés AVANZADO
  Origen    : $RutaOrigen
  Destino   : $RutaDestino
  Perfil    : $Perfil
  Intensidad: $Intensidad
  Escenario : $Escenario
  Hilos     : $NumHilosParalelos | Copiadores demonio: $Script:NumCopiadores

"@ -ForegroundColor White

foreach ($par in @(@{ N='Origen'; R=$RutaOrigen }, @{ N='Destino'; R=$RutaDestino })) {
    if (-not (Test-Path -LiteralPath $par.R)) {
        throw "No existe $($par.N): $($par.R)"
    }
}

$Script:RutaSmanagerResuelta = Get-RutaSmanager
if (-not $SoloGenerar -and -not $Script:RutaSmanagerResuelta) {
    throw 'No se encuentra smanager.exe'
}
if ($Script:RutaSmanagerResuelta) { Write-Metrica 'smanager.exe' $Script:RutaSmanagerResuelta }

if ($LimpiarOrigen -and (Confirmar-SiNecesario "¿Vaciar A ($RutaOrigen)?")) {
    Clear-CarpetaPrueba -Ruta $RutaOrigen -Etiqueta 'origen'
}
if ($LimpiarDestino -and (Confirmar-SiNecesario "¿Vaciar B ($RutaDestino)?")) {
    Clear-CarpetaPrueba -Ruta $RutaDestino -Etiqueta 'destino'
}

New-Item -ItemType Directory -Path $RutaOrigen, $RutaDestino -Force | Out-Null

$fases = switch ($Escenario) {
    'Pequenos'         { @('Pequenos') }
    'Ancho'            { @('Ancho') }
    'Profundo'         { @('Profundo') }
    'MezclaTamanos'    { @('MezclaTamanos') }
    'Grandes'          { @('Grandes') }
    'ColaSaturada'     { @('ColaSaturada') }
    'Rafaga'           { @('Rafaga') }
    'Paralelo'         { @('Paralelo') }
    'EscrituraLenta'   { @('EscrituraLenta') }
    'Renombres'        { @('Renombres') }
    'Modificaciones'   { @('Pequenos', 'Modificaciones') }
    'AppendContinuo'   { @('AppendContinuo') }
    'NombresComplejos' { @('NombresComplejos') }
    'Exclusiones'      { @('Exclusiones') }
    'CaosCaliente'     { @('Pequenos', 'CaosCaliente') }
    default {
        @(
            'Pequenos', 'Ancho', 'Profundo', 'MezclaTamanos', 'Grandes',
            'ColaSaturada', 'Rafaga', 'Paralelo', 'EscrituraLenta',
            'Renombres', 'Modificaciones', 'AppendContinuo', 'NombresComplejos',
            'Exclusiones', 'CaosCaliente'
        )
    }
}

$rutaConfig = New-ConfiguracionEstres
Write-Metrica 'Configuración' $rutaConfig

if (-not $SoloGenerar -and -not $NoIniciarDemonio) {
    Start-DemonioEstres -RutaConfig $rutaConfig
    Start-Sleep -Seconds 2
}

foreach ($fase in $fases) {
    $inicioFase = [datetime]::UtcNow
    $conteoAInicio = @(Get-ArchivosOrigenEsperados -Raiz $RutaOrigen).Count
    $conteoBInicio = @(Get-ArchivosOrigenEsperados -Raiz $RutaDestino).Count

    switch ($fase) {
        'Pequenos'         { Invoke-FasePequenos }
        'Ancho'            { Invoke-FaseAncho }
        'Profundo'         { Invoke-FaseProfundo }
        'MezclaTamanos'    { Invoke-FaseMezclaTamanos }
        'Grandes'          { Invoke-FaseGrandes }
        'ColaSaturada'     { Invoke-FaseColaSaturada }
        'Rafaga'           { Invoke-FaseRafaga }
        'Paralelo'         { Invoke-FaseParalelo }
        'EscrituraLenta'   { Invoke-FaseEscrituraLenta }
        'Renombres'        { Invoke-FaseRenombres }
        'Modificaciones'   { Invoke-FaseModificaciones }
        'AppendContinuo'   { Invoke-FaseAppendContinuo }
        'NombresComplejos' { Invoke-FaseNombresComplejos }
        'Exclusiones'      { Invoke-FaseExclusiones }
        'CaosCaliente'     { Invoke-FaseCaosCaliente }
    }

    $conteoADespues = @(Get-ArchivosOrigenEsperados -Raiz $RutaOrigen).Count
    $archivosNuevosFase = $conteoADespues - $conteoAInicio

    if ($SoloGenerar) {
        $resultadosFases.Add([pscustomobject]@{
            Fase = $fase; ArchivosNuevos = $archivosNuevosFase; SyncOk = $null
        })
        continue
    }

    Start-Sleep -Seconds ($Script:SegundosEstabilidad + 1)
    $soloCola = $fasesCaoticas -contains $fase
    $syncOk = Wait-SincronizacionCompleta `
        -ArchivosNuevosEnFase $archivosNuevosFase `
        -ConteoDestinoInicial $conteoBInicio `
        -TimeoutSegundos $TimeoutEsperaSegundos `
        -SoloColaVacia:$soloCola

    $integridad = Test-IntegridadOrigenDestino -MuestrasHash $(if ($fase -in @('Modificaciones', 'CaosCaliente', 'Pequenos')) { $MuestrasHashIntegridad } else { 0 })

    $resultadosFases.Add([pscustomobject]@{
        Fase           = $fase
        ArchivosNuevos = $archivosNuevosFase
        SyncOk         = $syncOk
        Integridad     = $integridad.Ok
        Faltantes      = $integridad.Faltantes.Count
        HashMal        = $integridad.HashIncorrecto.Count
        DuracionSeg    = [math]::Round(([datetime]::UtcNow - $inicioFase).TotalSeconds, 1)
    })

    Show-EstadoSmanager
    if (-not $integridad.Ok) {
        Write-Host "  AVISO fase '$fase'" -ForegroundColor Yellow
        $integridad.Faltantes | Select-Object -First 3 | ForEach-Object { Write-Host "    falta: $_" }
        $integridad.HashIncorrecto | Select-Object -First 3 | ForEach-Object { Write-Host "    hash: $_" }
    }
}

Write-Fase 'Informe final'
$duracionTotal = [math]::Round(([datetime]::UtcNow - $inicioTotal).TotalMinutes, 2)
$bytesOrigen = Get-SumaBytes -Raiz $RutaOrigen
$bytesDestino = Get-SumaBytes -Raiz $RutaDestino

Write-Metrica 'Duración (min)' $duracionTotal
Write-Metrica 'Archivos A' @(Get-ArchivosOrigenEsperados -Raiz $RutaOrigen).Count
Write-Metrica 'Archivos B' @(Get-ArchivosOrigenEsperados -Raiz $RutaDestino).Count
Write-Metrica 'Bytes A' $bytesOrigen
Write-Metrica 'Bytes B' $bytesDestino
Write-Metrica 'Cola pico global' $Script:ColaMaximaObservada

if (-not $SoloGenerar) {
    Show-EstadoSmanager
    $integridadFinal = Test-IntegridadOrigenDestino -MuestrasHash $MuestrasHashIntegridad
    if ($integridadFinal.Ok) {
        Write-Host "`nOK — integridad A→B (tamaños + hash muestra)." -ForegroundColor Green
    }
    else {
        Write-Host "`nFALLO — faltantes=$($integridadFinal.Faltantes.Count) hash=$($integridadFinal.HashIncorrecto.Count)" -ForegroundColor Red
        exit 1
    }
}

$resultadosFases | Format-Table -AutoSize

$rutaInforme = Join-Path ([Environment]::GetFolderPath('Desktop')) "SManager-estres-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
@"
SManager 2.0 — estrés avanzado
Intensidad: $Intensidad | Escenario: $Escenario
Duración (min): $duracionTotal
Archivos A/B: $(@(Get-ArchivosOrigenEsperados -Raiz $RutaOrigen).Count) / $(@(Get-ArchivosOrigenEsperados -Raiz $RutaDestino).Count)
Bytes A/B: $bytesOrigen / $bytesDestino
Cola pico: $Script:ColaMaximaObservada

$($resultadosFases | Format-Table | Out-String)
"@ | Set-Content -LiteralPath $rutaInforme -Encoding UTF8
Write-Host "Informe: $rutaInforme" -ForegroundColor DarkGray
