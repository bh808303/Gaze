# Transcribe interaction fixes

- Header cross invokes Finish exactly like Done; it is disabled while busy. Cancel remains explicit. System close/quit confirmation remains available through the existing close flow.
- Done uses explicit white text with red normal, hover, and pressed backgrounds, avoiding Fluent theme hover overrides.
- Active Transcribe tray left-click shows/restores/activates the existing window without finishing or discarding captures. Keyboard shortcut and main panel stop action remain stop actions.

Validation (2026-10-14):
- Release build to artifacts/transcribe-ui: exit 0, zero warnings/errors.
- `--transcribe-ui-check`: observed failing before the cross fix; exit 0 after. Checks hidden and minimized window restoration without Finish, and cross/Done invoking Finish without Cancel or close prompt.
- Gaze.Tests executable: exit 0, all 46 checks passed.
- Actual Transcribe preview rendered and inspected. Live tray click and pointer-hover verification remain manual.
