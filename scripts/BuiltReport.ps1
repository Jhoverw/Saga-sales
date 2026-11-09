# BuiltReport.ps1
# Compila la UI de Blazor, levanta el contenedor y muestra el build‑id.

$composeFile = "deploy\\docker-compose.yml"
$service = "blazor-web"

Write-Host "Stopping existing stack…" -ForegroundColor Cyan
$null = docker compose -f $composeFile down -v

Write-Host "Building and starting '$service' …" -ForegroundColor Cyan
$null = docker compose -f $composeFile up --build -d $service

# Esperar a que el contenedor esté healthy (máximo 30 s)
$maxAttempts = 30
$attempt = 0
while ($attempt -lt $maxAttempts) {
    $status = docker compose -f $composeFile ps $service ^| Select-String -Pattern "healthy|running"
    if ($status) { break }
    Start-Sleep -Seconds 1
    $attempt++
}

Write-Host "Fetching build id…" -ForegroundColor Cyan
$buildId = docker compose -f $composeFile exec $service cat /usr/share/nginx/html/assets/build-id.txt

Write-Host "Done!" -ForegroundColor Green
Write-Host "Build ID: $buildId" -ForegroundColor Yellow
Write-Host "URL: http://localhost:5008" -ForegroundColor Yellow
