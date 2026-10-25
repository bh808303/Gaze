# Transcribe live batches

Select **Transcribe** and click **Start**. A movable topmost session panel opens; an empty Start leaves the clipboard unchanged. Use the selected Transcribe trigger to capture the window under the pointer or drag an area within the starting monitor. Each successful capture publishes the complete ordered current batch as PNG files, ready to paste into a compatible destination. Transcribe always uses the clipboard, independently of AutoCopy.

The panel shows count, aggregate encoded PNG bytes, latest thumbnail and clipboard status. **Undo** removes the latest screenshot and republishes the current batch. Undoing the last screenshot clears only Gaze's own clipboard offering. **Next Batch** resets count, bytes, thumbnail and status, clears only an owned offering, and continues capturing. Paste the topic before starting the next batch. Old offered files stay available for pending readers until Gaze exits.

**ZIP** publishes only the current batch as an archive and closes the panel on success. It contains sequence/title-named PNGs and instructions.txt for ordered transcription, overlap merging, deduplication, headings and unreadable text without guessing. Undo can leave sequence gaps. Empty ZIP is disabled. **X**, main-panel **Stop**, the action shortcut and enabled double-Esc close without ZIP or prompts, preserving the current offering and its files. A later empty Start preserves that offering too.

## Limits and recovery

**Claude Desktop Clipboard** retains the existing persisted `SplitAt29Mb` setting and defaults off. For video it still produces parts of at most 29,000,000 bytes with the existing splitting/AutoCopy behavior. Transcribe snapshots it at Start:

- On: at most 20 images and 29,000,000 encoded PNG bytes (29 MB) per batch.
- Off: at most 100 images and 268,435,456 encoded PNG bytes (256 MiB) per batch.

Exact boundaries are accepted. A capture exceeding either limit is rejected with a persistent warning; the prior batch and clipboard stay intact. Use Next Batch or ZIP. ZIP with the toggle on must also fit 29,000,000 bytes including archive overhead; an otherwise valid PNG batch may fail ZIP. Split it into smaller batches. Limits bound retained encoded bytes, not peak memory during capture or ZIP construction.

Clipboard/staging failure keeps captures and shows an error with **Retry**. Capture retry publishes the current PNG list. Failed ZIP retry retries the current ZIP and closes only after success. Capturing another image, Undo or Next Batch supersedes failed ZIP intent; subsequent Retry publishes current PNGs (or retries clearing an empty batch), without closing. Copying unrelated content replaces the clipboard; the next successful capture republishes the full batch. Undo/Next Batch never clear unrelated clipboard data. Failed clear remains an error until recovery succeeds.

Temporary PNGs/ZIPs belong to Gaze, independently of the panel. Closing the panel does not delete them. Keep Gaze running until the destination finishes reading. Gaze startup cleans recognized stale artifacts from inactive instances; exit withdraws an owned offering and removes its own staged artifacts. Cleanup avoids reparse points, unrelated files, configured saved files and active instances. Failures are contained/reported and can leave artifacts for a later startup; there is no age rule or storage quota. Temporary clipboard paths become unavailable at Gaze exit. Transcribe does not save to the configured video folder.

## Validation limits

The historical standalone temporary-ZIP probe pasted into Claude Desktop 2.37337.1 on Windows build 26300 on 2026-10-14. That does not establish current live multi-PNG paste or ZIP ingestion support in every Claude version. No actual Claude upload or messages were performed for this live-batch change. Controller/panel fixtures use injected capture/clipboard boundaries, not physical input. Actual mouse gesture injection, integrated Claude paste/ingestion, tray shortcuts/video regression, mixed DPI and multiple monitors remain untested in this run. Windows capture-affinity exclusion is requested, not guaranteed for every environment. Protected/closed-window and memory-exhaustion recovery remain limited. See [final verification](factory/batches/verification.md).

Configured-folder **Clear** includes top-level MP4, PNG and ZIP (exact extensions, case insensitive), after a default-No confirmation showing count, types and folder. Cancel, empty or missing folders change nothing. Other extensions and nested files are preserved. This manual Recycle Bin action is separate from temporary Transcribe cleanup; the recordings gallery remains video-only.

## Window controls
Left-clicking the system tray icon restores and activates the existing Transcribe window without ending capture. The header cross closes capture while leaving current screenshots available to paste. ZIP keeps white text on a red background during hover and press.

Choose **Middle mouse** (default, including older settings) or **Print Screen** in Settings → Transcribe trigger, then Save. Cancel discards edits. The choice is exclusive and is frozen when a new Transcribe session starts. Middle mouse uses middle-click/middle-drag; Print Screen leaves middle mouse normal. Tap and release bare Print Screen to capture the window under the pointer on any monitor; hold it and move the pointer to select visible screen pixels. Release captures the area, clipped to the monitor where the gesture began. No extra click or hold timer is needed.

During a Print Screen session, bare Print Screen is reserved for capture. Modified combinations, including Ctrl+Alt+Print Screen start/stop, keep their normal shortcut behavior. Once a bare press is owned, it stays owned through release even if modifiers change. Esc cancels a selection. Repeats do not add captures. Ordinary input continues normally; Print Screen follows its normal path outside Transcribe and when Middle mouse is selected. Closing during a held key drains the owned release for up to five seconds; a key held longer, process termination or OS hook removal can escape this cleanup. Fn keys must deliver the Windows Print Screen key code. Native Windows Snipping interaction, physical input and mixed-DPI/multi-monitor operation remain deferred interactive checks; injected routing tests do not prove OS suppression.
