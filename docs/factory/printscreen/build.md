# PS01 implementation and validation

`TranscribeTrigger` is shared in Gaze.Core: `MiddleMouse=0`, `PrintScreen=1`.
`TranscribeTriggerPolicy` owns physical key pairs and returns normalized MMB down/move/up messages, or -1 for cancellation. It is independent of native APIs. The existing `TranscribeGesture` retains threshold, starting window/title and monitor clipping behavior.

`TranscribeInput(queue, trigger = MiddleMouse)` installs a session mouse hook and, only for PrintScreen, a keyboard hook. Keyboard coordinates come from GetCursorPos; target/title/monitor are snapshotted on down. Only the selected trigger is routed. Shift/Ctrl/Alt/Windows combinations pass through unless the session already owns that physical PrintScreen press. Repeats never restart selection. Initial held keys and unmatched releases pass through. Escape owns its cancellation pair. Hook callbacks queue normalized records; screenshot work remains outside callbacks.

Controller's final optional `trigger` constructor argument defaults to MMB, preserving existing callers. PS02 must persist/validate the enum and forward it from Settings through PanelOptions/host mappings to this argument, freeze it per session, and update session instructions and both settings UIs. No settings or UI choice has been added by PS01.

Cancellation increments an input generation so already queued down/move/up records cannot resurrect a canceled selection. Controller capture versions reject asynchronous results after cancellation/reset/close. Undo, Next Batch and close cancel pending selection; Escape clears selection without calling host stop.

## Closing with held keys

Dispose immediately stops capture routing and removes the mouse hook. If a PrintScreen or cancellation Escape release is still owned, the keyboard hook drains only those pairs (and PrintScreen repeats), then a 25ms host timer removes it. Cleanup has a five-second maximum. Future PrintScreen presses pass through while draining. A new session samples held state and cannot acquire an old press. Hook setup failures unhook successfully installed resources.

This is bounded best-effort native cleanup: a key held beyond five seconds can release after unhook and reach Windows. Process termination, a stopped host message pump, hook timeout/removal by Windows and firmware/Snipping behavior are not controlled by the policy. No persistent remap, synthetic key or registry change is used. The five-second native timer and actual suppression need interactive validation; pure policy tests establish ownership transitions only.

## Evidence

Run directory: `docs/factory/runs/20261025-161224-458`. Each check has separate command, output and exit files. `checks.ps1` records mandatory checks without changing repository validation scripts. BuildInParallel=false is set in the worker environment. The core RED used an API-only no-op scaffold, compiled and failed all four behavioral tests (exit 1); this demonstrates absent routing, gesture sequencing, cancellation and held-key ownership, rather than a missing API/compiler failure. Policy GREEN exits 0.

Tests cover exclusive defaults, direct down/move/up tap and drag with starting-target snapshots and negative-coordinate monitor clipping, repeat/unmatched release, modified passthrough including owned release after modifiers change, Escape pair ownership, stale canceled release, startup-held and teardown boundaries. Existing gesture tests cover additional threshold and geometry cases. Clipboard checks use injected publication boundaries and verify the real clipboard sequence is unchanged; no native keys were sent to other apps and no user files were removed.

Native validation gaps: Windows PrintScreen/Snipping suppression, actual Ctrl+Alt+PrintScreen registration interaction, firmware Fn behavior, pointer/overlay capture pixels and key-held close timing were not interactively exercised. Mixed-DPI and physical multi-monitor checks remain deferred as previously approved. No video pipeline changes.

Final check outcomes: Debug Build.ps1 0; Release Build.ps1 0 (both include core harness); settings 0; clipboard 0 (real clipboard sequence unchanged); Python 0 (five tests); final no-restore native Debug build 0; git diff --check 0. Build restores emitted NU1900 warnings because NuGet vulnerability metadata was unavailable; compilation completed with no errors. Native gaps above remain unverified, not implied by these automated results.

## PS01 review checkpoint
Run 20261025-161224-458 supplied credible behavioral RED/GREEN and passed mandatory checks, but independent review rejected incomplete coverage: Escape release test did not assert Message==0, and new tests did not exercise real controller generation/version rejection. This is a checkpoint, not an accepted completed slice. A fresh repair worker must add integration coverage and demonstrate mutation sensitivity while preserving original preimplementation RED evidence and exact source restoration. Native OS suppression remains unverified.
