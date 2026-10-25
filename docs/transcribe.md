# Transcribe live batches

Select **Transcribe** and click **Start**. A movable topmost session panel opens; an empty Start leaves the clipboard unchanged. Middle-click captures the window under the pointer; middle-drag captures visible pixels within the starting monitor. Each successful capture publishes the complete ordered current batch as PNG files, ready to paste into a compatible destination. Transcribe always uses the clipboard, independently of AutoCopy.

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
