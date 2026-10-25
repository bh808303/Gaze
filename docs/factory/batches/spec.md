# Specification: live Transcribe clipboard batches

## Objective
Efficiently capture configuration screens across menus and paste a topic-sized batch into an agent, then continue with a new batch. Approved alignment: alignment.md. Only Transcribe changes.

## Acceptance criteria
AC01 Start opens a topmost session panel; first successful capture publishes current ordered PNG file list. Starting an empty session leaves existing clipboard unchanged.
AC02 Captures retain sanitized sequence/title filenames and immutable bytes. Count/size refer only to current batch. Undo removes latest and republishes; last Undo clears only an owned offering. No capture gesture/video regressions.
AC03 Next Batch resets captures, bytes, status, and thumbnail; clears only an owned clipboard offering. It leaves session capturing. Previously offered files survive for pending readers until app exit. Failed clear must be reported, not falsely claimed successful; permit retry/current-state recovery.
AC04 ZIP creates ordered PNGs + existing instructions.txt from only current batch. Successful publication replaces clipboard and closes panel. Empty ZIP disabled. Failure keeps panel/captures and supports retry; no stale ZIP after Undo/Next Batch. Clipboard ownership outlives panel.
AC05 X/Stop/action shortcut/enabled double-Esc close session without ZIP or prompts, preserving current offering/backing files. A new session changes clipboard only on first successful capture. Explicit ZIP is the only finish/export action.
AC06 Every successful capture publishes full current batch even if user copied unrelated content. Publication failure retains new screenshot; panel clearly shows clipboard stale/error and retry publishes current batch, never stale queued captures. Undo/Next Batch cannot accidentally delete unrelated clipboard data.
AC07 Existing SplitAt29Mb persisted key becomes UI label Claude Desktop Clipboard in both panels. Video code/splitting behavior/AutoCopy remain unchanged. Transcribe ignores AutoCopy and snapshots this limit setting at start. Enabled: <=20 screenshots and <=29,000,000 aggregate encoded PNG bytes. Disabled: <=100 and <=268,435,456 bytes. Exact boundaries accepted; exceeding latest capture rejected with panel warning and unchanged prior offering. Final ZIP when toggle on must also fit 29,000,000 bytes, including archive overhead; failure retains batch and prior clipboard.
AC08 Panel shows count, total PNG size, latest thumbnail, clipboard status, Undo, Next Batch, ZIP, and clipboard retry. Topmost and capture exclusion maintained; warning must not vanish on normal refresh. Accessible button names and no clipped helper/status text.
AC09 Gaze startup/exit cleans only its own temporary Transcribe PNG/ZIP artifacts; panel closure doesn't. Saved configured-folder files untouched. Avoid reparse points, unrelated files, and another active instance. No age rule or retained-storage quota. Cleanup failures contained/reported; no broad destructive deletion.
AC10 Automated behavioral tests cover boundaries, reset/Undo, ownership overwrite, failures/retry, close vs ZIP, ZIP contents/current batch, temp cleanup safety, and persisted-setting compatibility. Debug/Release app and all existing core/settings/Python checks pass. Record actual feasible native validation, and mark Claude paste/ingestion or desktop checks untested if unavailable rather than claim success.

## Commands
- pwsh -NoProfile -File scripts/Build.ps1
- pwsh -NoProfile -File scripts/Build.ps1 -Release
- dotnet run --project tests/Gaze.Settings.Tests
- python -m unittest discover -s tests -p test_timeline_reader.py
- git diff --check

## Boundaries / architecture
Reuse capture APIs, gesture hooks, archive naming, OLE STA owner and existing saved-folder Clear. Model limits/reset in Gaze.Core. Stage PNGs in an app-owned temp directory and publish conventional multi-file drop. Keep staging lifecycle independent of window lifecycle. Use injection for meaningful native ownership/file failure tests; avoid dependencies. Do not alter video modes, encoder, click/keyboard metadata, updater, or unrelated features.

## Risks and validation limits
Batch size is encoded data, not peak RAM. Reject before retaining a new oversized PNG. File clipboard compatibility must be tested in destination; unit tests cannot establish Claude acceptance. App exit intentionally invalidates temporary clipboard files. Avoid cleanup racing another instance. ZIP overhead can exceed an otherwise valid PNG total; surface export error with captures retained.

## Authorization
User agreed full design after the interview and instructed building through Software Factory. No additional product decisions pending. Routine task decomposition/local commits and worker execution implement that authorization; no merge/deployment authorized.

## AC11: saved-folder Clear follow-up (2026-10-25)
Human answered the previously pending question: "Include PNG and ZIP alongside MP4, with confirmation". Clear now includes all top-level files whose exact extension is .mp4, .png or .zip (case insensitive) in the configured folder, after an explicit default-No confirmation naming scope/count/folder, using the existing Recycle Bin behavior. Exclude other extensions and subfolders. Cancel/empty/missing folder is a no-op. Automatic temp cleanup remains separate and never broadens into saved-folder cleanup. Do not change the recordings gallery or video capture/export. Test selection and confirmation/cancellation with injected recycle callbacks; no actual user files recycled during automated validation. This authorization supersedes the earlier pending-choice notes.
