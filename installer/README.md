# Windows MSI

Build the encoder with `./scripts/Build-Encoder.ps1`, then run `./scripts/Build-Installer.ps1`. Requires Git for Windows and .NET 10 SDK; the installer script installs WiX 4.0.6 into the ignored `.tools/wix` directory when needed. The default is Gaze's minimal FFmpeg 7.1/x264 build. Supply `-FfmpegPath` to select another executable supporting libx264, and supply its matching source and notices before redistribution. `-SkipPublish` repackages an existing self-contained payload and is intended for packaging validation only.

Output: `artifacts/installer/Gaze-0.7.0-alpha-win-x64.msi`.

The x64 per-machine installer requires Windows 10 build 19041 or later. It installs in Program Files/Gaze, includes the .NET desktop runtime and a private FFmpeg executable, registers a Start-menu shortcut, and supports Windows Installed apps uninstall. Installing does not replace system FFmpeg, change PATH, start Gaze automatically, or enable sign-in startup. `GAZE_FFMPEG` remains an explicit runtime override. User settings and recordings are not installer-owned and survive uninstall.

MSI uses product version 0.7.0; the application and display name identify the alpha. Future installers must increment the MSI version for normal major-upgrade behavior. The UpgradeCode remains stable. Packages are unsigned until a signing certificate is configured.

## Validation

The payload is self-contained, with includedFrameworks in the runtime configuration, and includes ffmpeg.exe beside Gaze.App.exe. Administrative extraction (`msiexec /a`) tests packaging without installing the product. A synthetic recording from the extracted payload checks window/dialog switching, saving, clipboard copying, decoded H.264 footage, and absence of audio. Full interactive installation, repair, upgrade, and uninstall testing on a clean Windows device remains a release gate.

## Redistribution

The MSI includes FFmpeg GPLv3, x264 GPLv2-or-later, WPF UI MIT, and .NET notices. FFmpeg is invoked as a separate executable. The installer release supplies the exact FFmpeg and x264 source archives and build scripts in `Gaze-0.7.0-alpha-encoder-source.zip`. See [ENCODER.md](ENCODER.md). Run `./scripts/Pack-EncoderSource.ps1` after building the MSI to create the source ZIP and SHA256 sums for both assets. Publish these three files together; upstream links alone are not a substitute for the corresponding source archive.

Sources used for packaging: [WiX package schema](https://docs.firegiant.com/wix/schema/wxs/package/) and [FFmpeg licensing](https://ffmpeg.org/legal.html).
