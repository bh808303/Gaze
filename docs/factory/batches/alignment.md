# Transcribe live batches: agreed alignment

2026-10-25, Europe/Amsterdam. Human completed the grilling interview and explicitly said: "the design is agreed, build it with the software factory". This authorizes implementation of the agreed design and necessary local Factory steps. No merge, deployment, or publication requested.

This extends the existing Transcribe feature, not video capture. Existing historical Factory records remain intact. The current design supersedes earlier Done/Cancel, AutoCopy routing, and session-close prompts for Transcribe only.

- Start Transcribe; open an always-on-top panel. Middle-click/drag screenshots accumulate in a current batch; publish all retained PNG paths to the clipboard after each capture, replacing other clipboard content.
- Next Batch clears only Gaze's current clipboard offering, resets the active batch, and keeps capturing. Never infer success from Ctrl+V. Keep earlier staged files available until Gaze exits.
- ZIP packages only the current batch plus instructions.txt, publishes the ZIP, and ends the session only after success. A new session leaves this ZIP untouched until its first successful screenshot.
- X, Stop, action shortcut, and enabled double-Esc end capture without ZIP, leaving already offered screenshots available. No discard prompt. Keep temporary files after panel close.
- Undo removes the latest screenshot and republishes remaining images; remove Gaze's offering if none remain. Clipboard failure retains capture and exposes retry of the current snapshot with clear status.
- Panel shows current count, total PNG size, latest thumbnail, clipboard status, Undo, Next Batch, ZIP, and retry when applicable. ZIP disabled when empty.
- Transcribe copying is independent of AutoCopy. Rename existing SplitAt29Mb toggle to Claude Desktop Clipboard, retaining persisted key and video split behavior. Helper explains video 29 MB parts and Transcribe 20 screenshots / 29 MB total batch.
- Toggle on: 20 screenshots and 29,000,000 total PNG bytes; check final ZIP against the same byte ceiling. Reject an over-limit latest capture, preserve previous batch/clipboard, and warn in panel. Toggle off: initial product guardrails 100 screenshots / 256 MiB encoded PNG data.
- Clean only Gaze-owned temporary Transcribe artifacts at Gaze startup/exit, never panel close, never saved files. Saved screenshots/ZIPs remain subject to configured-folder Clear. No total retained-storage cap; user manages it.
- Destination compatibility is distinct from Windows multi-file support. Windows file clipboard holds paths; no claim of automatic Claude upload/ingestion. Existing multi-monitor/mixed-DPI validation deferrals remain.

Implementation choices: freeze the Claude limit at session start; temporary PNG staging (explicitly inherent in the approved live clipboard design), no new permanent-save behavior or gallery. Preserve existing video behavior and settings serialization. Startup cleanup must avoid another live Gaze instance and reparse points/unrelated files.
