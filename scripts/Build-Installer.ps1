param([string]$FfmpegPath, [switch]$SkipPublish)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskOutput = Join-Path $taskRoot 'artifacts/installer'
$taskPayload = Join-Path $taskOutput 'payload'
$taskWix = Join-Path $taskRoot '.tools/wix/wix.exe'
if (-not $FfmpegPath) { $FfmpegPath = Join-Path $taskRoot '.tools/encoder-build/output/ffmpeg.exe' }
$FfmpegPath = (Resolve-Path -LiteralPath $FfmpegPath).Path
if (-not (Test-Path -LiteralPath $taskWix)) {
    dotnet tool install wix --version 4.0.6 --tool-path (Split-Path $taskWix -Parent)
    if ($LASTEXITCODE -ne 0) { throw 'WiX installation failed.' }
}
New-Item -ItemType Directory -Path $taskPayload -Force | Out-Null
if (-not $SkipPublish) {
    dotnet publish (Join-Path $taskRoot 'src/Gaze.App') -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=false -p:PublishTrimmed=false -p:UseSharedCompilation=false --packages (Join-Path $taskRoot '.packages') -o $taskPayload
    if ($LASTEXITCODE -ne 0) { throw 'Application publishing failed.' }
}
Copy-Item -LiteralPath $FfmpegPath -Destination (Join-Path $taskPayload 'ffmpeg.exe') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'installer/licenses') -Destination $taskPayload -Recurse -Force
$taskConfig = Get-Content (Join-Path $taskPayload 'Gaze.App.runtimeconfig.json') -Raw | ConvertFrom-Json
$taskRuntimeVersion = ($taskConfig.runtimeOptions.includedFrameworks | Where-Object name -eq 'Microsoft.NETCore.App').version
if (-not $taskRuntimeVersion) { throw 'Installer payload must be self-contained.' }
$taskRuntimePackage = Join-Path $taskRoot ('.packages/microsoft.netcore.app.runtime.win-x64/' + $taskRuntimeVersion)
Copy-Item -LiteralPath (Join-Path $taskRuntimePackage 'LICENSE.TXT') -Destination (Join-Path $taskPayload 'licenses/DotNet-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $taskRuntimePackage 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $taskPayload 'licenses/DotNet-ThirdPartyNotices.txt') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot ('.packages/microsoft.windowsdesktop.app.runtime.win-x64/' + $taskRuntimeVersion + '/LICENSE')) -Destination (Join-Path $taskPayload 'licenses/DotNet-WindowsDesktop-LICENSE.txt') -Force
foreach ($name in @('FFmpeg-GPLv3.txt', 'WPF-UI-MIT.txt')) {
    if ((Get-Item -LiteralPath (Join-Path $taskPayload ('licenses/' + $name))).Length -eq 0) { throw "Missing license text: $name" }
}
& $FfmpegPath -version | Set-Content (Join-Path $taskPayload 'licenses/FFmpeg-build.txt')
if ($LASTEXITCODE -ne 0) { throw 'Bundled FFmpeg is not executable.' }

function Escape-Xml([string]$value) { [Security.SecurityElement]::Escape($value) }
function Payload-Id([string]$relative) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($relative.ToLowerInvariant())
    $hash = [Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    'p' + ([BitConverter]::ToString($hash).Replace('-', '').Substring(0, 24))
}
$taskTree = [Text.StringBuilder]::new()
$taskRefs = [Text.StringBuilder]::new()
function Write-Payload([string]$directory) {
    foreach ($file in Get-ChildItem -LiteralPath $directory -File | Where-Object { $_.Extension -ne '.pdb' } | Sort-Object Name) {
        $relative = $file.FullName.Substring($taskPayload.Length + 1)
        $id = Payload-Id $relative
        [void]$taskTree.AppendLine('<Component Id="' + $id + '" Guid="*"><File Id="f' + $id + '" Source="' + (Escape-Xml $file.FullName) + '" KeyPath="yes" /></Component>')
        [void]$taskRefs.AppendLine('<ComponentRef Id="' + $id + '" />')
    }
    foreach ($child in Get-ChildItem -LiteralPath $directory -Directory | Sort-Object Name) {
        $id = Payload-Id $child.FullName.Substring($taskPayload.Length + 1)
        [void]$taskTree.AppendLine('<Directory Id="d' + $id + '" Name="' + (Escape-Xml $child.Name) + '">')
        Write-Payload $child.FullName
        [void]$taskTree.AppendLine('</Directory>')
    }
}
Write-Payload $taskPayload
$taskFragment = '<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"><Fragment><DirectoryRef Id="INSTALLFOLDER">' + $taskTree.ToString() + '</DirectoryRef><ComponentGroup Id="Payload">' + $taskRefs.ToString() + '</ComponentGroup></Fragment></Wix>'
$taskFragmentPath = Join-Path $taskOutput 'Payload.wxs'
$taskFragment | Set-Content -LiteralPath $taskFragmentPath -Encoding UTF8
$taskMsi = Join-Path $taskOutput 'Gaze-0.3.0-alpha-win-x64.msi'
& $taskWix build (Join-Path $taskRoot 'installer/Package.wxs') $taskFragmentPath -arch x64 -d ('IconPath=' + (Join-Path $taskRoot 'assets/gaze-icon-states/gaze-silent.ico')) -o $taskMsi
if ($LASTEXITCODE -ne 0) { throw 'MSI build failed.' }
Get-FileHash -LiteralPath $taskMsi -Algorithm SHA256 | Format-List
$taskDigest = (Get-FileHash -LiteralPath $taskMsi -Algorithm SHA256).Hash.ToLowerInvariant()
"$taskDigest  $([IO.Path]::GetFileName($taskMsi))" | Set-Content (Join-Path $taskOutput 'SHA256SUMS.txt') -Encoding ascii
Write-Output $taskMsi
