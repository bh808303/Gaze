param([switch]$Release)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $configuration = if ($Release) { 'Release' } else { 'Debug' }
    dotnet restore src/Gaze.App --packages .packages
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet build src/Gaze.App --no-restore -c $configuration -p:Platform=x64 -p:UseSharedCompilation=false -o artifacts/app
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet run --project tests/Gaze.Tests
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally { Pop-Location }
