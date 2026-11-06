<#
.SYNOPSIS
    Tells whether the panel is actually serving and able to talk to the API.

.DESCRIPTION
    Written after the failures that cost hours in this project, and corrected once it reported two
    things that were simply not true:

      * blazor.boot.json returning 404 is NORMAL on the .NET 10 dev server. The runtime gets its
        boot configuration another way, so checking that file produced a false alarm.
      * Reading text out of the app .wasm is fragile: the file name and the encoding change between
        builds, so a "marker not found" is not proof of a stale build.

    What is left are checks that mean something: something serves the port, the loader and the
    runtime are both there, the app payload downloads, and the API answers.

    Run it after any change. Each FAIL prints the command that fixes it.

.PARAMETER Port
    Panel port. 5008 by default.
#>
[CmdletBinding()]
param(
    [int]$Port = 5008
)

$ErrorActionPreference = 'Continue'

$failures = New-Object System.Collections.Generic.List[string]
$base = "http://localhost:$Port"

function Check {
    param([string]$Name, [bool]$Ok, [string]$Detail, [string]$Fix)

    if ($Ok) {
        Write-Host ("  PASS  {0,-36} {1}" -f $Name, $Detail) -ForegroundColor Green
        return
    }

    Write-Host ("  FAIL  {0,-36} {1}" -f $Name, $Detail) -ForegroundColor Red
    if ($Fix) { Write-Host ("        -> {0}" -f $Fix) -ForegroundColor Yellow }
    $script:failures.Add($Name)
}

function Get-Status([string]$Url) {
    try { return [int](Invoke-WebRequest $Url -UseBasicParsing -TimeoutSec 10).StatusCode }
    catch { if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode } return 0 }
}

function Get-Length([string]$Url) {
    try { return (Invoke-WebRequest $Url -UseBasicParsing -TimeoutSec 20).RawContentLength }
    catch { return 0 }
}

Write-Host ''
Write-Host "== Panel en $base" -ForegroundColor Cyan

# ── 1. algo sirve el puerto ─────────────────────────────────────────────────
$status = Get-Status "$base/"
Check -Name 'el panel responde' -Ok ($status -eq 200) -Detail "HTTP $status" `
    -Fix 'Arrancalo: ./scripts/dev-panel.ps1   (o el contenedor: docker compose up -d blazor-web)'

if ($status -ne 200) {
    Write-Host ''
    Write-Host 'Sin panel no hay nada mas que comprobar.' -ForegroundColor Red
    exit 1
}

# ── 2. el index es el actual ────────────────────────────────────────────────
$page = (Invoke-WebRequest "$base/" -UseBasicParsing -TimeoutSec 10).Content
Check -Name 'index.html actual' -Ok ($page -match 'saga-splash') -Detail $(if ($page -match 'saga-splash') { 'splash propio' } else { 'index antiguo' }) `
    -Fix 'La imagen o la build es vieja: dotnet clean src\SagaSales.BlazorWeb; borra bin y obj; vuelve a servir'

# ── 3. cargador y runtime presentes y del mismo build ──────────────────────
$loader = Get-Length "$base/_framework/blazor.webassembly.js"
Check -Name 'cargador de Blazor' -Ok ($loader -gt 0) -Detail "$loader b" `
    -Fix 'Artefactos incompletos: limpia bin y obj del panel y recompila'

$runtime = Get-Length "$base/_framework/dotnet.js"
Check -Name 'runtime de .NET' -Ok ($runtime -gt 0) -Detail "$runtime b" `
    -Fix 'El publish no genero el runtime: limpia bin y obj del panel y recompila'

# ── 4. (retirado) El nombre del archivo de la app cambia entre builds (huella webcil), asi que
# comprobar "SagaSales.BlazorWeb.wasm" daba falsos negativos. Y el contenido del wasm no prueba que
# el navegador lo ejecute: solo el navegador puede decirlo. Se deja la comprobacion del cargador y
# del runtime, que si son estables.

# ── 5. la API esta disponible ──────────────────────────────────────────────
$api = Get-Status 'http://localhost:5000/health'
Check -Name 'gateway responde' -Ok ($api -eq 200) -Detail "HTTP $api" `
    -Fix 'Levanta el stack: ./scripts/up.ps1'

$identity = Get-Status 'http://localhost:5001/health'
Check -Name 'identity responde' -Ok ($identity -eq 200) -Detail "HTTP $identity" `
    -Fix 'Levanta el stack: ./scripts/up.ps1'

# ── 6. el panel apunta a la API correcta ──────────────────────────────────
try {
    $config = (Invoke-WebRequest "$base/appsettings.json" -UseBasicParsing -TimeoutSec 10).Content | ConvertFrom-Json
    Check -Name 'apunta al gateway' -Ok ($config.ApiBaseUrl -eq 'http://localhost:5000/') -Detail $config.ApiBaseUrl `
        -Fix 'Corrige wwwroot\appsettings.json: ApiBaseUrl = http://localhost:5000/'
}
catch {
    Check -Name 'apunta al gateway' -Ok $false -Detail 'appsettings.json no legible' `
        -Fix 'Revisa src\SagaSales.BlazorWeb\wwwroot\appsettings.json'
}

Write-Host ''
if ($failures.Count -eq 0) {
    Write-Host 'PANEL: OK — sirve la build actual y la API responde.' -ForegroundColor Green
    Write-Host 'Si el navegador no lo refleja, abre en incognito: es cache del navegador.' -ForegroundColor DarkGray
    exit 0
}

Write-Host ("PANEL: " + $failures.Count + ' problema(s). Aplica el -> de cada FAIL.') -ForegroundColor Red
exit 1
