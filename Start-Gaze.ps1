$ErrorActionPreference = 'Stop'
$taskExecutable = Join-Path $PSScriptRoot 'artifacts/app/Gaze.App.exe'
$taskEncoder = Join-Path (Split-Path $taskExecutable -Parent) 'ffmpeg.exe'
if (-not (Test-Path -LiteralPath $taskExecutable) -or -not (Test-Path -LiteralPath $taskEncoder)) {
    & (Join-Path $PSScriptRoot 'scripts/Build.ps1') -Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
Start-Process -FilePath $taskExecutable -WindowStyle Hidden -WorkingDirectory $PSScriptRoot
