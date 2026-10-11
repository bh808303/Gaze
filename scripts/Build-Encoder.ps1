param([string]$GitDirectory = 'C:/Program Files/Git')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskWork = Join-Path $taskRoot '.tools/encoder-build'
New-Item -ItemType Directory -Path $taskWork -Force | Out-Null
$taskDownloads = @(
    @('ffmpeg-n7.1.tar.gz', 'https://codeload.github.com/FFmpeg/FFmpeg/tar.gz/refs/tags/n7.1', '7DDAD2D992BD250A6C56053C26029F7E728BEBF0F37F80CF3F8A0E6EC706431A'),
    @('x264.tar.gz', 'https://codeload.github.com/mirror/x264/tar.gz/31e19f92f00c7003fa115047ce50978bc98c3a0d', 'D053C9D86988D6BC78237CA5205865C5DDF99C98EF4CD9927EEC8F6D388F6DD9'),
    @('llvm-mingw.zip', 'https://github.com/mstorsjo/llvm-mingw/releases/download/20261006/llvm-mingw-20261006-ucrt-x86_64.zip', '317492C456AA27EE607A5919F1D2D38DCDC1112516A24D0BF4B00D078F52D17A'),
    @('nasm.zip', 'https://www.nasm.us/pub/nasm/releasebuilds/2.16.03/win64/nasm-2.16.03-win64.zip', '3EE4782247BCB874378D02F7EAB4E294A84D3D15F3F6EE2DE2F47A46AA7226E6'),
    @('make.tar.zst', 'https://repo.msys2.org/msys/x86_64/make-4.4.1-2-x86_64.pkg.tar.zst', '2408AF61717DAE87B00C855B132769A125C708907FC94A46BB16DAE076113E5C')
)
foreach ($download in $taskDownloads) {
    $path = Join-Path $taskWork $download[0]
    if (-not (Test-Path -LiteralPath $path)) {
        & curl.exe -sS -L --fail --retry 2 --max-time 300 $download[1] -o $path
        if ($LASTEXITCODE -ne 0) { throw "Download failed: $($download[0])" }
    }
    if ((Get-FileHash $path -Algorithm SHA256).Hash -ne $download[2]) { throw "Checksum mismatch: $path" }
}
foreach ($name in @('toolchain', 'nasm')) {
    $archive = if ($name -eq 'toolchain') { 'llvm-mingw.zip' } else { 'nasm.zip' }
    if (-not (Test-Path (Join-Path $taskWork $name))) {
        Expand-Archive (Join-Path $taskWork $archive) -DestinationPath (Join-Path $taskWork $name)
    }
}
foreach ($name in @('make', 'sources/ffmpeg', 'sources/x264')) {
    $destination = Join-Path $taskWork $name
    if (-not (Test-Path $destination)) {
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        $archive = switch ($name) { 'make' { 'make.tar.zst' } 'sources/ffmpeg' { 'ffmpeg-n7.1.tar.gz' } 'sources/x264' { 'x264.tar.gz' } }
        if ($name -eq 'make') { & tar.exe -xf (Join-Path $taskWork $archive) -C $destination }
        else { & tar.exe -xf (Join-Path $taskWork $archive) --strip-components 1 -C $destination }
        if ($LASTEXITCODE -ne 0) { throw "Extraction failed: $archive" }
    }
}
foreach ($dll in @('msys-2.0.dll', 'msys-intl-8.dll', 'msys-iconv-2.dll')) {
    Copy-Item (Join-Path $GitDirectory "usr/bin/$dll") (Join-Path $taskWork 'make/usr/bin') -Force
}
# A temporary drive alias keeps spaces out of upstream makefiles. Never replace
# an existing drive mapping; always remove our own alias when the build exits.
$taskDrive = @('Z', 'Y', 'X', 'W') | Where-Object { -not (Test-Path "${_}:/") } | Select-Object -First 1
if (-not $taskDrive) { throw 'No free temporary drive letter (W through Z).' }
& subst.exe "${taskDrive}:" $taskWork
if ($LASTEXITCODE -ne 0) { throw 'Could not map encoder workspace.' }
try {
    Copy-Item (Join-Path $PSScriptRoot 'build-encoder.sh') (Join-Path $taskWork 'build-encoder.sh') -Force
    & (Join-Path $GitDirectory 'bin/bash.exe') -lc "cd /$($taskDrive.ToLowerInvariant()) && bash build-encoder.sh > build.log 2>&1"
    if ($LASTEXITCODE -ne 0) { throw "Encoder build failed. See $taskWork/build.log" }
} finally { & subst.exe "${taskDrive}:" /d }
Write-Output (Join-Path $taskWork 'output/ffmpeg.exe')
