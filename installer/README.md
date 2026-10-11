# Windows MSI

Build with `./scripts/Build-Installer.ps1`. Requires .NET 10 SDK; the script installs WiX 4.0.6 into the ignored `.tools/wix` directory when needed. Supply `-FfmpegPath` to select the encoder executable; the default is the local development FFmpeg 7.1 binary. It must support libx264. `-SkipPublish` repackages an existing self-contained payload and is intended for packaging validation only.

Output: `artifacts/installer/Gaze-0.3.0-alpha-win-x64.msi`.

The x64 per-machine installer requires Windows 10 build 19041 or later. It installs in Program Files/Gaze, includes the .NET desktop runtime and a private FFmpeg executable, registers a Start-menu shortcut, and supports Windows Installed apps uninstall. Installing does not replace system FFmpeg, change PATH, start Gaze automatically, or enable sign-in startup. `GAZE_FFMPEG` remains an explicit runtime override. User settings and recordings are not installer-owned and survive uninstall.

MSI uses product version 0.3.0; the application and display name identify the alpha. Future installers must increment the MSI version for normal major-upgrade behavior. The UpgradeCode remains stable. Packages are unsigned until a signing certificate is configured.

## Validation

The payload is self-contained, with includedFrameworks in the runtime configuration, and includes ffmpeg.exe beside Gaze.App.exe. Administrative extraction (`msiexec /a`) tests packaging without installing the product. A synthetic recording from the extracted payload checks window/dialog switching, saving, clipboard copying, decoded H.264 footage, and absence of audio. Full interactive installation, repair, upgrade, and uninstall testing on a clean Windows device remains a release gate.

## Redistribution

The local MSI includes upstream FFmpeg GPLv3, WPF UI MIT, and .NET notices. FFmpeg is invoked as a separate executable. The supplied encoder is a Gyan GPLv3 build with external libraries. Before uploading this MSI publicly, distribute its complete corresponding source, including the matching external library sources and build scripts. The upstream source links in NOTICE.txt alone do not satisfy that requirement. This installer has not been attached to the public alpha release.

Sources used for packaging: [WiX package schema](https://docs.firegiant.com/wix/schema/wxs/package/), [FFmpeg licensing](https://ffmpeg.org/legal.html), and [Gyan build provenance](https://www.gyan.dev/ffmpeg/builds/).
