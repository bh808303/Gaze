# Transcribe mode

Select **Transcribe** in Gaze's capture dropdown and click **Start**. This opens a movable session window without arming a video target or creating an MP4.

- Middle-click a window to capture that window. Each gesture chooses a new target.
- Middle-drag to capture a rectangle of visible screen pixels. The rectangle stays within the monitor where the drag began; its title comes from the starting window.
- The session shows the latest screenshot and capture count. **Undo** removes only the latest retained screenshot.
- **Done**, the main panel's **Stop**, and the action shortcut finish the batch. Enabled double-Esc also stops. Empty sessions close without exporting.
- **Cancel** discards. Closing a nonempty session or quitting offers **Finish**, **Discard**, or **Keep capturing**. Export failure keeps the session open and exposes **Retry**.

AutoCopy is frozen at session start. When enabled, Gaze constructs PNG/ZIP bytes in memory, writes only the final ZIP under an app-owned temporary directory, and offers that ZIP through the conventional file-copy clipboard. There is no permanent copy in the video folder. Keep Gaze running until the receiving app has finished reading the attachment. Closing the screenshot session leaves the clipboard owner alive in Gaze. Gaze removes its own staged files on graceful process exit after withdrawing its own offering; failures may leave an orphan. A replacement clipboard referencing the same ZIP prevents cleanup. Crashes/forced termination may leave temporary ZIPs; no automatic sweep deletes old instances or unrelated files. Copying other clipboard content replaces the offering.

With AutoCopy disabled, Gaze saves a uniquely named ZIP in the configured video folder. It refuses collisions rather than overwriting existing files. A failed write/export preserves the retained captures for retry. No empty batch changes the clipboard or writes an archive.

The ZIP contains ordered PNGs named with capture sequence and sanitized window title, plus instructions.txt for ordered transcription, overlap merging, deduplication, heading preservation, and flagging unreadable text without guessing. Undo can leave sequence gaps.

## Validation and limits

Human testing on 2026-10-14 confirmed that the temporary-file probe pasted into Claude Desktop 2.37337.1 on Windows build 26300. Claude read the ZIP, instructions and PNG marker correctly. The original virtual in-memory clipboard approach did not attach. This is compatibility evidence for the probe in that installation, not universal paste support or completed integrated-export validation.

Single-monitor integrated native fixture checks passed for middle-click/drag, exact rectangle pixels, Undo, ZIP export, ordinary mouse/keyboard delivery, close choices, Retry and hook teardown. Full tray-host quit/video regression, integrated Claude paste, and broader target/exclusion cases remain incomplete; consult docs/factory/evidence/integration.md. Multi-monitor and mixed-DPI scenarios remain deferred and untested. Capture UI/feedback exclusion is requested through Windows capture affinity; this is not a guarantee for every capture method or environment. Ordinary capture failures preserve the previous batch. Protected/closed windows and resource-specific recovery remain limited. PNGs and ZIP construction consume memory proportional to batch size, with temporary copies during encoding/export; configurable batch limits are deferred. OS/process memory exhaustion is not recoverable by guarantee.

## Window controls
The header cross finishes the session just like Done. While a Transcribe session is active, left-clicking the system tray icon restores and activates its existing window without ending capture. Done keeps white text on a red background during hover and press. Cancel discards explicitly.
