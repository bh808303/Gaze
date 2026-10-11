$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskWork = Join-Path $taskRoot '.tools/encoder-build'
$taskStage = Join-Path $taskRoot 'artifacts/encoder-source'
New-Item -ItemType Directory -Path $taskStage -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $taskStage 'scripts') -Force | Out-Null
foreach ($name in @('ffmpeg-n7.1.tar.gz', 'x264.tar.gz', 'build.log')) {
    Copy-Item (Join-Path $taskWork $name) $taskStage -Force
}
foreach ($name in @('config.h', 'config.mak', 'FFmpeg-build.txt')) {
    Copy-Item (Join-Path $taskWork "output/$name") $taskStage -Force
}
Copy-Item (Join-Path $PSScriptRoot 'Build-Encoder.ps1'), (Join-Path $PSScriptRoot 'build-encoder.sh') (Join-Path $taskStage 'scripts') -Force
Copy-Item (Join-Path $taskRoot 'installer/ENCODER.md') (Join-Path $taskStage 'README.md') -Force
Copy-Item (Join-Path $taskWork 'sources/ffmpeg/COPYING.GPLv3') (Join-Path $taskStage 'FFmpeg-GPLv3.txt') -Force
Copy-Item (Join-Path $taskWork 'sources/x264/COPYING') (Join-Path $taskStage 'x264-GPLv2.txt') -Force
$taskZip = Join-Path $taskRoot 'artifacts/installer/Gaze-0.3.0-alpha-encoder-source.zip'
Compress-Archive -Path (Join-Path $taskStage '*') -DestinationPath $taskZip -Force
$taskFiles = @('Gaze-0.3.0-alpha-win-x64.msi', 'Gaze-0.3.0-alpha-encoder-source.zip')
$taskLines = foreach ($name in $taskFiles) {
    $hash = (Get-FileHash (Join-Path $taskRoot "artifacts/installer/$name") -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
$taskLines | Set-Content (Join-Path $taskRoot 'artifacts/installer/SHA256SUMS.txt') -Encoding ascii
Write-Output $taskZip
