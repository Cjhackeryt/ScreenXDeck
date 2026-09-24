param(
    [string]$MacroDeckCli = "macrodeck-plugin"
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$distDir = "$root\dist"
$manifest = Get-Content "$root\src\ScreenxDeck\manifest.json" -Raw | ConvertFrom-Json
$packageFile = "$distDir\$($manifest.id)-$($manifest.version).macrodeckplugin"

$cli = Get-Command $MacroDeckCli -ErrorAction SilentlyContinue
if ($null -eq $cli) {
    throw "Macro Deck CLI was not found. Install it with 'dotnet tool install --global MacroDeck.Plugin.Cli --prerelease'."
}

New-Item -ItemType Directory -Path $distDir -Force | Out-Null

Write-Host "Building ScreenxDeck $($manifest.version)..." -ForegroundColor Cyan
& $cli.Source build --source "$root\src\ScreenxDeck" --output $distDir --force --no-color
if ($LASTEXITCODE -ne 0) {
    throw "ScreenxDeck build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path $packageFile)) {
    throw "Expected package was not created: $packageFile"
}

Write-Host "Inspecting package..." -ForegroundColor Cyan
& $cli.Source inspect --artifact $packageFile --output Text --no-color
if ($LASTEXITCODE -ne 0) {
    throw "ScreenxDeck package inspection failed with exit code $LASTEXITCODE."
}

$fileInfo = Get-Item $packageFile
Write-Host "Successfully packaged: $packageFile" -ForegroundColor Green
Write-Host ("Package Size: {0:N2} KB" -f ($fileInfo.Length / 1KB)) -ForegroundColor Green
