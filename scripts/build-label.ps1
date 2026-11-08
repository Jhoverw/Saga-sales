<#
.SYNOPSIS
    Computes the build label that the Web panel shows (version, git commit, dirty flag, UTC time).

.DESCRIPTION
    Sets $env:SAGA_BUILD_LABEL. deploy/docker-compose.yml passes it to the blazor-web image as a
    build argument, and the image writes it to assets/build-id.txt. The panel footer and the
    sidebar read that file, so the number on screen always matches the image that is running.

    up.ps1 and refresh.ps1 dot-source this script before building. Run it alone to see the label
    that the next build will produce:

        ./scripts/build-label.ps1

.EXAMPLE
    . ./scripts/build-label.ps1     # sets $env:SAGA_BUILD_LABEL in the current session
#>
[CmdletBinding()]
param()

$repoRoot = Split-Path -Parent $PSScriptRoot

# Semantic version: first line of SagaSales.version, for example "2.2.1-stable" -> "2.2.1".
$version = 'dev'
$versionFile = Join-Path $repoRoot 'SagaSales.version'
if (Test-Path $versionFile) {
    $first = (Get-Content $versionFile -TotalCount 1).Trim()
    if ($first -match '^(\d+\.\d+\.\d+)') { $version = $Matches[1] }
}

# Git commit. A dirty tree (any modified or untracked file) is flagged, because the image then
# contains code that the commit does not describe.
$sha = (& git -C $repoRoot rev-parse --short HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sha)) {
    $sha = 'nogit'
    $dirty = '-dirty'
}
else {
    $sha = $sha.Trim()
    $changes = @(& git -C $repoRoot status --porcelain 2>$null)
    $dirty = if ($changes.Count -gt 0) { '-dirty' } else { '' }
}

# Ecuador (GMT-5, no daylight saving): the team reads times in this zone.
$stamp = (Get-Date).ToUniversalTime().AddHours(-5).ToString('yyyy-MM-dd HH:mm')
$label = "v$version-$sha$dirty | $stamp GMT-5"

$env:SAGA_BUILD_LABEL = $label
Write-Host "  etiqueta de build: $label" -ForegroundColor DarkGray
