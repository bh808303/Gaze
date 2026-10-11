# Changelog

## Unreleased

- Keep Compact's 10-second maximum keyframe interval when re-encoding split parts; Original remains at 2 seconds. Continue checking final part sizes after metadata.

- Order output timeline events by frame and clock time, retaining processing order for equal timestamps, including split parts and carried-over context.

- Retain clicks between Resume and the first resumed sample, preserving their clock times and assigning them to that resumed frame, including across split boundaries.

- Queue mouse-hook clicks without waiting for drawing or timeline locks. Drain accepted clicks before frame advancement, pause and stop; defer warning file writes to the encoder worker outside the drawing lock.

- Protect event titles containing backslashes/newlines from subtitle conversion using versioned base64 payloads. The timeline reader supports legacy JSON and warns/skips malformed packets instead of losing the entire timeline.

- Automatically embed foreground process/window-title chapters, a disabled event text track with independently captured mouse clicks, and exact event/start/resume clock timestamps with UTC offsets. Preserve original titles and enable metadata automatically.
- Rebase events and carry active-window context into split parts; check the 29 MB limit after metadata is added. Retain playable plain video and log a warning if embedding fails.
- Supply Read-Timeline.py and FFprobe to extract all chapters, events and clocks in one command. Rebuild the private FFmpeg with FFmetadata/SRT/SubRip/movtext support and package matching complete source and checksums.

Local event-timeline build, 2026-10-11: matching encoder source is `artifacts/app-event-timeline/Gaze-0.3.0-alpha-encoder-source.zip`; the portable build contains SHA256SUMS.txt. No public release or installed MSI was replaced.

| Artifact | SHA256 |
| --- | --- |
| ffmpeg.exe | `6a07a5bd49bb406815ba5e72ae48ecf9b170d4acf0ab475b817aed285ad68f52` |
| ffprobe.exe | `a7bd6e8a80b180c19279a6e77bbb7874045f862a3f11c7d677269fbc32314fee` |
| Encoder source ZIP | `1b6782df1d2bb343d959add8272c6d2de11196750b8981fb07cbb924ed30dbd1` |

- Show the 29 MB split toggle in the main tray settings panel and preserve it when saving panel settings.

- Optional Settings → Saving split for recordings over 29 MB: verified numbered MP4 parts, all-part automatic clipboard copying, and original retention if splitting fails. Splitting re-encodes the video and requires the updated bundled encoder.

- Extend Highlight mouse clicks to Main monitor and All monitors recordings, including displays positioned left of or above the primary display.

- Choose Original or Compact compression in Capture settings. Compact preserves resolution and frame rate with smaller files, at the cost of detail and encoder CPU time; no fixed upload size is guaranteed. Compact uses a maximum 10-second keyframe interval to reduce repeated full images on static screens; seeking can take longer.

## 0.3.0-alpha installer — 2026-10-11

- Self-contained x64 MSI with .NET runtime, private FFmpeg, Start-menu shortcut and uninstall registration.
- Minimal FFmpeg/x264 build with matching complete source archives and build scripts supplied alongside the installer.
- SHA256 checksums for the MSI and encoder source package.

The installer is unsigned. Administrative extraction and synthetic recording are checked locally; clean-device interactive install, repair, upgrade and uninstall testing remain outstanding.

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
