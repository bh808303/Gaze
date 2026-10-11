# Changelog

## 0.3.0-alpha — 2026-10-11

- Windows tray recording for a selected window, the main monitor, or all monitors.
- Tray-click and configurable shortcut controls; default Ctrl + Alt + PrintScreen.
- Owned-window following, pause when minimized, and double-Esc stop confirmation.
- Silent video recording at 5 fps by default, with configurable frame rate and window canvas.
- MP4 saving, automatic clipboard file copying, and recording-folder actions.
- WPF UI panel with supplied Gaze artwork, bold recording selection, and aligned buttons.
- Software rendering and hidden-first placement to address blank panels and opening flashes.
- Dispose the recording panel after saving; recreate it on the next tray opening.
- Removed interrupted-recording recovery and startup recovery prompts.

This is an alpha source release. Windows behavior has been checked locally; broader hardware and monitor configurations still need validation. FFmpeg is an external runtime dependency and is not included in the release.
