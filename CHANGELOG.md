# Changelog

## Persistent Output and file categories — 2026-10-25

- Use Output/Videos for new video recordings and Output/Screenshots for accepted PNGs and final batch ZIPs. Preserve the Folder setting and legacy files; no automatic migration.
- Save uniquely, retry failures without duplicate output, and retain saved history through Undo, Next Batch, close and exit. Temporary clipboard staging remains separate.
- Add Screenshots/Recordings file categories defaulting to capture mode. Selected file actions, folder and default-No confirmed Clean-up follow the viewed category; only matching top-level files are recycled.


## Screenshot mode label — 2026-10-25

- Rename the user-facing Transcribe mode and trigger labels to Screenshot; retain existing saved settings and capture behavior.

## Transcribe trigger — 2026-10-25

- Add a persisted exclusive Middle mouse / Print Screen Transcribe trigger, both settings surfaces, selected-session hints, and controlled save/cancel/routing regression evidence. Native Snipping and physical multi-monitor checks remain deferred.

## Live Transcribe batches — 2026-10-25

- Extend configured-folder Clear to top-level MP4, PNG and ZIP files with a default-No confirmation before moving them to the Recycle Bin; preserve other files and subfolders.

- Publish ordered PNG batches on capture, with Undo, Next Batch, explicit ZIP and close without export. Retain temporary offerings until Gaze exit and safely clean stale artifacts at startup.
- Rename the existing split setting to Claude Desktop Clipboard; preserve its key and video behavior. Transcribe uses 20 images / 29 MB when enabled, 100 / 256 MiB otherwise, independently of AutoCopy.
- Retain captures and errors on clipboard/ZIP failure. Retry repeats failed ZIP until successful or superseded by capture/Undo/Next Batch. Show actionable limit warnings.
- Automated and injected panel validation does not establish current Claude ingestion or native tray/video acceptance; see docs/factory/batches/verification.md.


## Transcribe development — 2026-10-14

- Add Transcribe mode with a separate screenshot session, middle-click window capture, middle-drag monitor-clipped rectangles, latest thumbnail/count, Undo, Done/Stop, Cancel and close choices.
- Add ordered in-memory ZIP construction and filename sanitization. AutoCopy stages only a final temporary ZIP for conventional file-drop clipboard; AutoCopy off saves uniquely in the configured folder. Retain captures on export failure for Retry.
- Preserve video-mode enum values, append Transcribe as 3, and keep clipboard ownership independent of session-window lifetime. Prevent export reentry and avoid unused clipboard-owner threads for empty sessions.
- Add core, export/settings and native fixture checks. Single-monitor native capture/input/dialog checks pass; integrated Claude, tray quit/video regression and deferred multi-monitor/mixed-DPI validation remain incomplete. See docs/factory/evidence/integration.md.

## Unreleased — 2026-10-13

- Add **Record timeline metadata**, off by default for new/legacy settings and frozen at recording startup. Preserve the basic timeline and separate keyboard opt-in.
- Add factual button releases, signed raw vertical/horizontal wheel deltas, bounded changed-position pointer samples and neutral boundary transitions. Preserve original timestamps and capture/hit-test restrictions; drain accepted events on pause/stop and exclude paused input.
- Extend MP4 timeline schema to v2 with collection options/status, exact sample offsets and deduplicated capture geometry/mapping events, retaining v1/legacy cue reader compatibility. Carry geometry and boundary context into split parts.
- Batch dense frame events below timed-text packet limits. Add event kind, clock-time and local-frame reader filters with context, preserving safe plain-video fallback.
- Add red-green tests for defaults, settings/recording options, pointer rates/privacy, bounded queue loss, resume/split context, timing, dense serialization and reader filtering; verify real MP4 packet retention. Native desktop behavior and silent Windows hook removal remain outside this validation.

- Add Start at Windows sign-in in Settings, defaulting off unless Gaze is registered. Read actual current-user registration, quote executable paths, remove only Gaze on disable, and report failures. Preserve silent tray startup without automatic recording.
- Re-save the enabled switch after moving a portable executable to repair its path. Reject dotnet development-host registration. Add isolated startup tests; actual Windows sign-in execution remains unverified.

## 0.4.0-alpha installer — 2026-10-11

- Add off-by-default Keyboard events in timeline to the WPF settings. Record individual key-down/up events, repeats, virtual/scan codes and original timestamps only within the capture boundary; ignore paused input and retain resume-gap keys in split parts. Keep the keyboard hook queue-only.
- Wrap large base64 event cues safely and tolerate base64 whitespace when reading, preventing SubRip long-line wrapping from dropping busy event batches.

- Recognize editable combo dropdowns when their inner Edit child has focus by trying only its direct recorded parent and retaining the exact hwndList match. Verify a second native DropDown combo alongside DropDownList.

- Retain ownerless native menu (`#32768`) and combo dropdown (`ComboLBox`) clicks when Windows associates their active menu owner or exact focused-combo list with the recorded window. Add native regression steps holding both popups open across frame ticks; unrelated windows remain excluded.

- Add a guarded native fixture step that right-clicks the recorded window, selects a context-menu item and verifies both clicks survive timeline extraction while unrelated covering-window clicks remain excluded.

- Ignore location-change notifications in window capture and child-control moves in monitor capture. Cache foreground monitor-boundary state so ordinary moves do not reread process/title details. Window-mode queued clicks also reject unrelated covering windows when processed.

- Restrict timeline titles to the recorded window/owned dialogs or windows overlapping captured monitors. Replace foreign foreground context with neutral external events without identity fields; preserve this boundary through pause and split carry-over. Exclude clicks in uncaptured desktop gaps.

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
