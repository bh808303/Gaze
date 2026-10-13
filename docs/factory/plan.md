# Implementation plan: Transcribe capture mode

Status: awaiting human plan approval. No implementation authorized by this plan until approval.

## Basis and boundaries

Read FACTORY-TASK.md and docs/factory/spec.md, including the explicit 2026-10-14 specification approval and appended review decisions. Root AGENTS.md is absent. Repository content was used as evidence, not as independent authorization. Existing timeline records remain in previous-timeline-metadata; unrelated working-tree changes are preserved. This plan and tasks.json are the active Transcribe records.

Each task fits one fresh worker session (roughly 1–2 hours, at most five implementation/test files); read these records and the mapped criteria before starting. If a task exceeds this bound, split it and request approval of materially changed scope. No configurable memory limits, OCR, gallery, special protected-window workflow, disk spill, cross-monitor rectangles, merge or publishing.

## Design and gate decisions

Use a separate Transcribe lifecycle rather than forcing screenshots through video Session/Capture. Append the mode value; preserve serialized video values. Keep batch/gesture/archive logic testable in Gaze.Core, native capture/input/clipboard in Gaze.App and session UI in Gaze.Panel. A session snapshots AutoCopy/folder settings and allows one capture operation at a time. Use Windows drag metrics, padded sortable sequence filenames and a brief post-capture border flash; exact padding/duration are implementation details.

T01 proves cursor-free Windows Graphics Capture frames including occluded content and visible-screen rectangles. T02 proves OLE virtual-file export and actual Claude Desktop support before production integration. No new package is planned. Failed feasibility requires reporting evidence and reopening the affected design, without silent downgrade or disk fallback. Confirm API details against official platform documentation during these workers.

Proposed action semantics for plan approval: idle Transcribe displays Start and bypasses arming; active displays Stop. Existing configured action shortcut follows Start/Stop; enabled double-Esc finishes through Stop. Consume but ignore capture middle gestures originating on Gaze controls so the capture UI is not a target. Ordinary controls remain functional. Quit uses approved Finish / Discard / Keep capturing and failed Finish prevents quit. These proposals resolve remaining Start/shortcut integration uncertainty and are subject to this approval gate.

## Tasks

### T01: Prove native screenshot feasibility

Dependencies: none. Spec criteria: AC03,AC04,AC07,AC14. Scope: S/M, one fresh worker.

Files likely touched: src/Gaze.App/TranscribeCaptureProbe.cs; src/Gaze.App/TranscribeScreenshotSource.cs; src/Gaze.App/Program.cs; docs/factory/evidence/capture.md.

- [ ] Create a bounded screenshot probe using existing Windows Graphics Capture for cursor-free full-window PNGs and visible-pixel rectangle capture; do not wire production controls.
- [ ] Record occluded-window freshness, starting-monitor clipping, mixed DPI and UI exclusion results with OS/hardware versions in docs/factory/evidence/capture.md.
- [ ] Preserve process/batch on ordinary failures; if approved capture semantics cannot be met, stop the affected dependency chain and surface the limitation.

PowerShell verification:

```powershell
pwsh -NoProfile -File scripts/Build.ps1; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Native checks named above are manual desktop validation, additional to this command; command success never substitutes for them.

### T02: Prove virtual ZIP compatibility with Claude Desktop

Dependencies: none. Spec criteria: AC11,AC13. Scope: S/M, one fresh worker.

Files likely touched: src/Gaze.App/TranscribeClipboard.cs; src/Gaze.App/Program.cs; docs/factory/evidence/clipboard.md.

- [ ] Create an in-memory virtual-file clipboard probe using Windows OLE FileGroupDescriptorW/FileContents; keep ownership on a pumping STA independent of the session window.
- [ ] Validate actual Claude Desktop attachment/paste and ZIP ingestion separately, recording app/OS versions, file-write observations and clipboard lifetime in docs/factory/evidence/clipboard.md.
- [ ] No image/ZIP/temp spill or disk fallback; incompatibility blocks dependent production export pending user export-design review.

PowerShell verification:

```powershell
pwsh -NoProfile -File scripts/Build.ps1; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Native checks named above are manual desktop validation, additional to this command; command success never substitutes for them.

### T03: Model retained captures and lifecycle

Dependencies: none. Spec criteria: AC06,AC10,AC13,AC14. Scope: S/M, one fresh worker.

Files likely touched: src/Gaze.Core/TranscribeSession.cs; tests/Gaze.Tests/TranscribeChecks.cs; tests/Gaze.Tests/Program.cs.

- [ ] Implement independently testable in-memory batch states for capture, Undo, discard, finish, failed export and retry; empty finish causes no export.
- [ ] Retain immutable capture bytes/title/sequence; invalid areas and recoverable failures do not increment count; Undo removes only latest retained item.
- [ ] Tests cover close/quit Finish, Discard, Keep capturing and failed Finish preventing quit; document controller contracts.

PowerShell verification:

```powershell
dotnet run --project tests/Gaze.Tests; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Native checks named above are manual desktop validation, additional to this command; command success never substitutes for them.

### T04: Classify middle gestures and monitor clipping

Dependencies: T03. Spec criteria: AC03,AC04,AC05,AC06. Scope: S/M, one fresh worker.

Files likely touched: src/Gaze.Core/TranscribeGesture.cs; tests/Gaze.Tests/TranscribeChecks.cs.

- [ ] Implement pure gesture classification with Windows drag metrics supplied by adapter, title/window/monitor snapshotted at gesture start and no target lock.
- [ ] Clip rectangle to starting monitor, normalize reversed drags and ignore unusable area; window click targeting remains unrestricted across monitors.
- [ ] Tests cover threshold boundaries, negative coordinates, reversed drags, monitor edges and session cancellation.

PowerShell verification:

```powershell
dotnet run --project tests/Gaze.Tests; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Native checks named above are manual desktop validation, additional to this command; command success never substitutes for them.

### T05: Build ordered ZIP contents

Dependencies: T03. Spec criteria: AC08,AC09. Scope: S/M, one fresh worker.

Files likely touched: src/Gaze.Core/TranscribeArchive.cs; tests/Gaze.Tests/TranscribeArchiveChecks.cs; tests/Gaze.Tests/Program.cs.

- [ ] Produce ZIP in memory with valid PNGs in retained sequence order and instructions.txt containing all five transcription rules.
- [ ] Generate unique sortable sequence/title names for repeated, empty, reserved, invalid and long titles; rectangle uses starting title.
- [ ] Tests decode sample PNGs and assert archive entries, Undo ordering, sanitization and instruction content.

PowerShell verification:

```powershell
dotnet run --project tests/Gaze.Tests; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Native checks named above are manual desktop validation, additional to this command; command success never substitutes for them.

### T06: Deliver export routes and retry

Dependencies: T02, T03, T05. Spec criteria: AC10,AC11,AC12,AC13. Scope: S/M, one fresh worker.

Files likely touched: src/Gaze.App/TranscribeExporter.cs; src/Gaze.App/TranscribeClipboard.cs; tests/Gaze.Tests/TranscribeExportChecks.cs; tests/Gaze.Tests/Gaze.Tests.csproj; tests/Gaze.Tests/Program.cs.

- [ ] Route AutoCopy on to proven virtual ZIP publisher, off to configured folder with collision-safe creation; snapshot route at session start.
- [ ] Export exceptions retain batch and expose Retry; never fall back to disk, overwrite existing files or mutate clipboard for empty batches.
- [ ] Inject route failures and verify retry contains same retained ordered captures; verify clipboard ownership survives session UI disposal.

PowerShell verification:

```powershell
pwsh -NoProfile -File scripts/Build.ps1; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Native checks named above are manual desktop validation, additional to this command; command success never substitutes for them.

### T07: Add session window

Dependencies: T03. Spec criteria: AC02,AC06,AC10,AC13. Scope: S/M, one fresh worker.

Files likely touched: src/Gaze.Panel/TranscribeWindow.xaml; src/Gaze.Panel/TranscribeWindow.xaml.cs; src/Gaze.Panel/TranscribeViewState.cs; docs/factory/evidence/window.md.

- [ ] Create movable non-topmost WPF session window matching panel dimensions, logo, margins and style; show count, latest thumbnail, Undo, Done and Cancel.
- [ ] Bind state/controller callbacks including Retry and Finish / Discard / Keep capturing close prompt; do not add gallery.
- [ ] Verify empty Undo, thumbnail replacement, close choices and matching appearance at normal/mixed DPI; record screenshots in repository.

PowerShell verification:

```powershell
pwsh -NoProfile -File scripts/Build.ps1; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Native checks named above are manual desktop validation, additional to this command; command success never substitutes for them.

### T08: Connect consumed gestures to screenshots

Dependencies: T01, T03, T04. Spec criteria: AC03,AC04,AC05,AC06,AC07,AC14. Scope: S/M, one fresh worker.

Files likely touched: src/Gaze.App/TranscribeInput.cs; src/Gaze.App/TranscribeOverlay.cs; src/Gaze.App/TranscribeScreenshotSource.cs; src/Gaze.App/TranscribeController.cs; docs/factory/evidence/gestures.md.

- [ ] Install session-scoped dedicated middle hook, keeping callbacks short and forwarding other input; release hooks on all session exit paths.
- [ ] Display clipped selection then remove selection/UI feedback before capture; add brief border flash after successful capture without sound; failed captures preserve prior batch.
- [ ] Desktop checks cover different windows/monitors, drag across windows, left/double click/drag, keyboard/wheel, stale frames and hook teardown; record exclusion limitations.

PowerShell verification:

```powershell
pwsh -NoProfile -File scripts/Build.ps1; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Native checks named above are manual desktop validation, additional to this command; command success never substitutes for them.

### T09: Expose Transcribe mode and settings compatibility

Dependencies: T03. Spec criteria: AC01,AC15. Scope: S/M, one fresh worker.

Files likely touched: src/Gaze.Core/Session.cs; src/Gaze.App/Settings.cs; src/Gaze.Panel/PanelWindow.xaml; src/Gaze.Panel/PanelWindow.xaml.cs; tests/Gaze.Settings.Tests/Program.cs.

- [ ] Append Transcribe enum value preserving 0/1/2; add choice to both WPF and WinForms mode lists and validate settings round-trip.
- [ ] Show Start for idle Transcribe and Stop for active session, avoiding video arming labels; retain existing video labels/settings.
- [ ] Core/settings tests assert serialized compatibility and dropdown mappings; compile both panels.

PowerShell verification:

```powershell
dotnet run --project tests/Gaze.Settings.Tests; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Native checks named above are manual desktop validation, additional to this command; command success never substitutes for them.

### T10: Wire host start, stop and quit

Dependencies: T06, T07, T08, T09. Spec criteria: AC01,AC05,AC10,AC13,AC15. Scope: S/M, one fresh worker.

Files likely touched: src/Gaze.App/TrayContext.cs; src/Gaze.App/TrayPanel.cs; src/Gaze.App/TranscribeController.cs; tests/Gaze.Tests/TranscribeChecks.cs; docs/factory/evidence/integration.md.

- [ ] Wire single Transcribe session through TrayContext/TrayPanel: Start bypasses video target arming and encoder; Done and Stop call identical finish path.
- [ ] Proposed integration: configured action shortcut uses same Start/Stop path; enabled double-Esc uses Stop; middle gestures over Gaze controls are consumed but ignored as capture targets. Plan approval approves these choices; preserve ordinary controls.
- [ ] Wire close/Cancel/quit choices, failed Finish keeping app alive, disabled reentrant actions and hook teardown; verify no MP4 creation and representative video modes.

PowerShell verification:

```powershell
pwsh -NoProfile -File scripts/Build.ps1; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Native checks named above are manual desktop validation, additional to this command; command success never substitutes for them.

### T11: Validate integrated native capture and lifecycle

Dependencies: T10. Spec criteria: AC01,AC02,AC03,AC04,AC05,AC06,AC07,AC10,AC14,AC15. Scope: S/M, one fresh worker.

Files likely touched: docs/factory/evidence/native-validation.md; docs/factory/evidence/ desktop assets.

- [ ] Run integrated desktop matrix for occlusion, other-monitor window capture, starting-monitor rectangle clipping, mixed DPI, gestures, controls, Undo, feedback/exclusion and recoverable capture failure.
- [ ] Exercise empty finish, Cancel, close/quit choices and repeated start/stop; verify video Window/AllMonitors/CurrentMonitor and existing settings/export.
- [ ] Record versions, exact scenarios, expected/actual outcomes and evidence paths in docs/factory/evidence/native-validation.md; unavailable paths remain untested and block claims of full acceptance.

PowerShell verification:

```powershell
pwsh -NoProfile -File scripts/Build.ps1; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Native checks named above are manual desktop validation, additional to this command; command success never substitutes for them.

### T12: Validate export and document user behavior

Dependencies: T10. Spec criteria: AC08,AC09,AC11,AC12,AC13,AC14,AC15. Scope: S/M, one fresh worker.

Files likely touched: README.md; docs/transcribe.md; docs/factory/evidence/export-validation.md.

- [ ] Verify integrated Claude Desktop paste/attachment and ingestion separately, ZIP PNG decoding/order/instructions, session-close lifetime and process-exit limitation; observe no clipboard-route image/ZIP/temp writes.
- [ ] Exercise folder collisions, clipboard/folder failure and Retry with retained contents; record app/OS versions and destination outcomes in docs/factory/evidence/export-validation.md.
- [ ] Update README/user docs for gestures, routes, tested compatibility, capture limits and large-batch memory risk; mark unmet/untested criteria and run final mandatory checks.

PowerShell verification:

```powershell
pwsh -NoProfile -File scripts/Build.ps1; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Native checks named above are manual desktop validation, additional to this command; command success never substitutes for them.

## Dependency order and checkpoints

1. T01–T03: feasibility and lifecycle checkpoint. Review screenshot and Claude results before dependent production work; unsupported semantics block that chain.
2. T04–T06: geometry/archive/export checkpoint; meaningful regression and failure-injection tests pass.
3. T07–T09: UI/input/mode checkpoint; build, settings compatibility and native input checks pass.
4. T10: integrated action checkpoint; approve no new scope implicitly and verify quit/retry behavior.
5. T11–T12: final evidence checkpoint; every AC has results, mandatory checks pass, untested or failing paths are disclosed. Do not mark native acceptance complete based solely on build results.

Dependencies in tasks.json are authoritative task scheduling prerequisites, not evidence of completion. T01/T02/T03 may be worked independently; T07/T08/T09 follow their prerequisites. Shared files require sequential work even where dependencies permit concurrency. No workers are launched in this planning task.

## Acceptance-criterion coverage

| Spec criterion | Tasks |
|---|---|
| AC01 | T09, T10, T11 |
| AC02 | T07, T11 |
| AC03 | T01, T04, T08, T11 |
| AC04 | T01, T04, T08, T11 |
| AC05 | T04, T08, T10, T11 |
| AC06 | T03, T04, T07, T08, T11 |
| AC07 | T01, T08, T11 |
| AC08 | T05, T12 |
| AC09 | T05, T12 |
| AC10 | T03, T06, T07, T10, T11 |
| AC11 | T02, T06, T12 |
| AC12 | T06, T12 |
| AC13 | T02, T03, T06, T07, T10, T12 |
| AC14 | T01, T03, T08, T11, T12 |
| AC15 | T09, T10, T11, T12 |

## Mandatory checks

Run from repository root. Every command is PowerShell with explicit native exit propagation. Build.ps1 restores .packages, builds x64 into artifacts/app and runs Gaze.Tests; do not redundantly rerun core harness after a successful build absent new changes. Settings harness and timeline-reader regression are separate. Run Debug at integration checkpoints and Release plus all checks for final acceptance. There is no identified lint/coverage gate.

```powershell
pwsh -NoProfile -File scripts/Build.ps1; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
pwsh -NoProfile -File scripts/Build.ps1 -Release; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project tests/Gaze.Settings.Tests; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
python -m unittest discover -s tests -p test_timeline_reader.py; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
git diff --check; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Focused harness commands currently execute the whole harness; add meaningful regression cases to existing runners, not nonexistent test filters. Native validation needs an interactive Windows desktop and actual Claude Desktop; record unavailable environments accurately. Store all probes, outputs, screenshot assets and logs under this repository (evidence under docs/factory/evidence; builds use existing artifacts/.packages). For clipboard-only validation, evidence screenshots/logs are deliberate test artifacts, not exported batch spill.

## Risks and completion policy

Occluded capture freshness, exclusion and mixed DPI need runtime proof. Low-level hook callbacks must not block on encoding/capture. OLE ownership must survive closing the session window and remain alive on STA until clipboard replacement or process exit. Large batches can exhaust memory; configurable limits remain deferred. Specialized capture failures remain deferred, while recoverable exceptions must retain the batch.

completedTaskIds starts empty. Add an ID only after its code, tests and required native evidence pass; unavailable native checks keep evidence tasks incomplete. Worker handoff records implemented files, commands/results, mapped criteria and limitations here. Final completion requires both validation tasks and all prerequisite tasks, with no unsupported destination silently treated as accepted.

## Approval request

Approve this plan, including the proposed Start/shortcut and Gaze-control gesture semantics, before implementation. Specification approval is recorded; plan approval is not. Local feature work only.


## Human plan approval
On 2026-10-14 the user replied exactly "approve" after the plan summary and explicit request to approve this plan to start implementation. This authorizes local implementation and approved manifest checks, including capture/Claude Desktop feasibility gates and proposed Start/shortcut semantics. No merge, publishing or deployment authorized.

## Human-approved validation adjustment
On 2026-10-14 the human replied "agreed" to continuing single-monitor implementation/testing while deferring unavailable multi-monitor and mixed-DPI validation. Those unavailable checks and denied hardware/driver metadata queries are not prerequisites for implementation or completion of the current task. Run all feasible single-monitor behavioral capture checks and required automated checks; retain approved capture behavior, record unavailable scenarios as deferred/untested, and do not claim complete multi-monitor validation. Evidence records supersede the earlier T01 prerequisite-blocking interpretation. No weakening of feasible tests or implementation behavior is authorized.
