# Gaze 0.3 alpha

Gaze is a Windows tray application for recording readable screen sessions. Capture a selected window, the main monitor, or all monitors, then paste the saved file into another application.

## Controls

- Left-click the tray icon: arm → start recording → stop and save.
- Right-click: open the recording panel and Settings.
- Default shortcut: Ctrl + Alt + PrintScreen; configurable in Settings.
- Press Esc twice within one second to stop. The first press remains available to the application being recorded.

The tray icon is green when ready, orange when armed or saving, and red when recording.

## Recording

Video only, with no audio. The default is 5 frames per second, preserving native text resolution. Selected-window capture follows owned dialogs, returns to the original when they close, and stops when the original closes. Window canvas options are Windows size and Monitor size. Main monitor captures the Windows primary display; All monitors captures the desktop across displays.

Saved names use `Gaze_d101026_t2105-2107_2min.mp4`: local date and start/end times, with whole recorded minutes rounded down. Pauses are excluded from duration. A numbered suffix prevents overwriting files.

Automatic clipboard copying places a file reference on the clipboard. Open, Open Folder, Copy Clipboard, and Clean-up are available in the panel. Clean-up asks for confirmation and moves MP4 files directly in the recording folder to the Recycle Bin; subfolders and unfinished working files are retained.

Interrupted-recording recovery is not supported. Normal startup is silent. The idle panel can be captured by screenshot tools; it is excluded while armed, recording, or saving on supported Windows versions.

## Build and run

Requires Windows 10 version 2004 or later, x64, and the .NET 10 SDK specified by `global.json`.

```powershell
./scripts/Build.ps1 -Release
```

Place `ffmpeg.exe` beside `artifacts/app/Gaze.App.exe`, or set `GAZE_FFMPEG` to its executable path. Gaze requires an FFmpeg build with the libx264 encoder. The development build also copies a local encoder from `.tools` when available; this directory is excluded from Git.

Launch `artifacts/app/Gaze.App.exe`. The optional `Start-Gaze.ps1` launcher builds when the expected application or adjacent encoder is missing. Only one tray instance runs at a time.

## Alpha status and validation

This release publishes source code; FFmpeg and compiled application packages are not included. WPF UI 4.3.0 provides the interface controls. The panel uses software rendering and is positioned while hidden to avoid blank windows and placement flashes. It is recreated after saving or a recording failure.

Nine core checks cover recording names, target locking, paused duration, saving state, and Esc confirmation. Local synthetic recording checks cover decoded video, dialog following, overlay exclusion, and clipboard copying. Desktop rendering checks cover panel placement and recreation. Local evidence, recordings, build outputs, and dependencies are excluded from Git.

Broader GPU/DPI/multiple-monitor validation, browser-spawn attribution, and Windows sign-in startup remain outside this alpha's guarantees. Application transcription or AI analysis is not part of Gaze.

See [CHANGELOG.md](CHANGELOG.md) and [requirements](docs/requirements.md).

## Local MSI installer

Run ./scripts/Build-Installer.ps1 to produce a self-contained x64 MSI with a private FFmpeg copy, bundled .NET runtime, Start-menu shortcut, and uninstall registration. See installer/README.md for installation behavior, validation, and the corresponding-source gate before public redistribution. The current GitHub alpha remains a source release; the local MSI is not uploaded.
