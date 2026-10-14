# T01 current verification summary
All feasible native assertions pass in runs/20261014-t01-review-fixes/dpi-correction/resize-progress/local-native/result.txt: full window, fresh occluded content, visible rectangle, clipping, UI exclusion, both cursor positive controls, growth/shrink, acquisition failure and recovery. Native diagnostic exit code was 0.
Independent final-review.md found no capture-code blocker. It requested evidence reconciliation and exact local Build.ps1 Debug/Release passing records. These are being collected in local-mandatory-debug.log/.exit.txt and local-mandatory-release.log/.exit.txt in that review-fixes run. Do not mark T01 complete until both exit codes are 0.
Approved multi-monitor/mixed-DPI/hardware-metadata deferrals remain untested. Historical statuses below are superseded by this summary; preserve them as chronological evidence.

## Historical evidence
## T01 resize progress follow-up (2026-10-14)

The [parent local result](../runs/20261014-t01-review-fixes/dpi-correction/local-native/result.txt) passes base capture semantics, both cursor controls (clean=0/enabled=287) and constructor recovery; only grow/shrink time out. The screenshot source now pulls frames serially until the bounded deadline, removing dropped FrameArrived notifications and lastCopy throttling from this one-shot path. Size-mismatched frames remain unpublished; native frame/surface disposal precedes pool recreation. Cursor controls and cleanup remain intact.

Managed resize-progress, resource/frame safety, core/settings and Python tests pass. Serial Debug/Release app builds and native harness build pass with zero warnings/errors. Required Build.ps1 Debug/Release runners exit 1 at parallel restore in this environment; prescribed serial restore succeeds. These supplemental builds do not convert those runner failures into passes. Native growth/shrink is pending local execution; sandbox WGC 0x0 is an established environment limit. Approved monitor/DPI/hardware deferrals are unchanged and are not blockers. See [diagnosis and checks](../runs/20261014-t01-review-fixes/dpi-correction/resize-handoff.md) and [exact next local command](../runs/20261014-t01-review-fixes/dpi-correction/next-local-resize.ps1). No later task or completion claim.
# T01 native screenshot feasibility — current status (2026-10-14 review fixes)

Production fixes are retained: constructor acquisition unwinds resources/subscriptions, and resize-transition frames are discarded until surface and content dimensions match. No production controls are wired. T01 has not been marked complete: the extended native probe still needs execution with local permissions.

T01 test correction: [parent-native result](../runs/20261014-t01-review-fixes/parent-native/result.txt) passed the window cursor control (clean=0/enabled=287) but failed the rectangle control (clean=0/enabled=0) at DPI 120, skipping later checks. The standalone harness now declares PerMonitorV2 with the app manifest, verifies effective awareness, and places/verifies/restores the physical pointer. Cursor, growth, shrink and constructor checks report independently, retaining failure exit status and the unchanged zero-clean/nonzero-enabled requirement. Debug/Release builds and managed core/settings/resource-resize checks pass. The [corrected restricted run](../runs/20261014-t01-review-fixes/dpi-correction/restricted-native/result.txt) verifies PerMonitorV2 but still stops at a 0x0 WGC item; corrected native cursor/resize/constructor outcomes remain untested. See [correction handoff](../runs/20261014-t01-review-fixes/dpi-correction/handoff.md) and [exact next local command](../runs/20261014-t01-review-fixes/dpi-correction/next-local-native.ps1).

| Scenario | Current evidence |
|---|---|
| Full-window, occluded freshness, visible rectangle, monitor clipping, UI affinity exclusion, retained bytes and recovery | Passed in the [parent local diagnostic](../runs/20261014-005733-564/local-diagnostic/result.txt), before these review fixes. Preserve this historical pass; it is not a pass for the extended probe. |
| Constructor cleanup and resize publication policy | Meaningful managed regression [RED](../runs/20261014-t01-review-fixes/regression-red.txt), then [GREEN](../runs/20261014-t01-review-fixes/regression-green.txt). Uses the production ownership scope and frame admission policy; simulated acquisition stages do not prove native driver resource accounting. |
| Cursor-free window and rectangle, pointer inside | Extended probe compares pointer-outside/inside images, requires zero changed clean sample pixels and nonzero cursor-enabled positive-control pixels. Compiled; not reached in restricted run. |
| Native growth/shrink during capture and constructor failure/recovery | Extended probe allocates the pool before resizing, compares returned dimensions against stable capture, checks newly grown content, and injects failure after native acquisition. Compiled; not reached in restricted run. |
| Current native execution | [Exit 1: capture item 0x0](../runs/20261014-t01-review-fixes/native/result.txt). No new native pass claimed. |
| Automated checks | Serial restore, Debug/Release builds (zero warnings/errors), core/settings harnesses and five Python tests pass. Exact commands, logs and outcomes in [handoff](../runs/20261014-t01-review-fixes/handoff.md). Both exact Build.ps1 runs fail at restricted parallel restore, recorded separately. |
| Multi-monitor, mixed DPI, hardware metadata | Approved deferred/untested; do not block on these. |

AC14 production batch/count integration remains outside T01's probe. Large batches consume memory; no clipboard disk spill is introduced. The following sections preserve earlier evidence and interpretations chronologically; the current table above supersedes their status labels.

## Historical initial prerequisite failure

2026-10-14. Required command: `pwsh -NoProfile -File scripts/Build.ps1; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }`.

The required build exited 1 during restore, before compilation or acceptance-test execution. Diagnostic restore also exited 1 (Build FAILED, zero reported errors). Root cause is undetermined; no project.assets.json was available for Gaze.App. This is a missing build prerequisite, not a meaningful red test proving missing screenshot behavior. No green result exists. Stop the affected screenshot dependency chain until restore/build works; production controls and capture behavior remain unchanged.

The draft native acceptance fixture is preserved in ../runs/20261014-005100-746/TranscribeCaptureProbe.cs for resumption. It checks decoded PNG colors for full-window occluded freshness and visible rectangle pixels, clipped dimensions, capture-affinity exclusion, and recovery after an invalid handle with retained previous bytes. It has not compiled or executed. No screenshot implementation was added.

Environment from dotnet --info: Windows 10.0.26300, win-x64; SDK 10.0.303; .NET/WindowsDesktop runtime 10.0.11. Hardware and driver versions unavailable/unqueried.

| Scenario | Result |
|---|---|
| Cursor-free full-window PNG | Untested: build prerequisite |
| Occluded-window freshness | Untested: build prerequisite |
| Visible-pixel rectangle | Untested: build prerequisite |
| Starting-monitor clipping (single monitor) | Untested: build prerequisite |
| UI exclusion | Untested: build prerequisite |
| Ordinary failure/process and batch preservation | Untested: build prerequisite |
| Multi-monitor and mixed DPI | Deferred/untested under human-approved validation adjustment; not completion prerequisites |
| Hardware/driver versions | Deferred/unavailable; not completion prerequisites |

Exact build and diagnostic restore commands, outputs and exit codes are retained in ../runs/20261014-005100-746/. No native result is claimed. Large screenshot batches consume memory; this task introduces no persistence or clipboard disk spill.

## Restore recovery
Parent diagnosis on 2026-10-14: `dotnet restore src/Gaze.App --packages .packages --verbosity minimal` executed with local permissions returned exit 0 and restored Gaze.Core, Gaze.Panel and Gaze.App. The previous zero-error restore failure occurred under the worker's restricted environment; this comparison suggests an environment/access cause but does not identify the exact denied operation. Dependencies/assets now exist in this worktree. Reuse the saved acceptance fixture from docs/factory/runs/20261014-005100-746/TranscribeCaptureProbe.cs and proceed with meaningful behavioral RED/GREEN. Do not report prerequisite failure as RED. If restricted parallel restore fails despite cached assets, diagnose with supported single-process restore (`dotnet restore src/Gaze.App --packages .packages --disable-parallel -p:BuildInParallel=false`) before declaring blocked; preserve required checks and escalate specific access needs rather than silently weakening checks.

# T01 native screenshot feasibility — blocked

Run: `docs/factory/runs/20261014-005733-564` (2026-10-14).

The bounded implementation is opt-in via `Gaze.App.exe --transcribe-capture-probe <evidence-directory>`. No production session controls are wired. It uses the existing WGC/D3D11 interop pattern in a separate source with cursor capture disabled, new per-request sessions, a three-second frame deadline, in-memory PNG encoding, and visible monitor-frame crops intersected with the starting monitor's physical bounds. No clipboard or batch persistence is introduced. Probe PNG writes are explicit diagnostic output only.

## Observed environment and results

OS API: Microsoft Windows NT 10.0.26300.0; .NET runtime 10.0.11; one monitor; fixture DPI 120 (125%). Hardware model/GPU/driver versions: unavailable and explicitly untested/deferred under the human-approved validation adjustment. No inference about driver support is made from the OS version.

The acceptance harness first failed with exit 1 because `TranscribeScreenshotSource` did not exist. This represents missing native screenshot behavior, rather than an intentionally failing assertion. See `red-command.txt`, `red-output.txt`, `red-exit.txt`, and `red-native/result.txt`.

After implementation, the same acceptance test fails with exit 1: WGC reports support but the fixture capture item has size 0x0; creating the frame pool raises an invalid-parameter exception. Explicitly restoring the fixture and allowing it to paint did not resolve this. Root cause is unresolved; this evidence does not establish whether the limitation is environmental or in the capture implementation. A separate hidden launch of the app executable returned an unavailable-window error; hidden-launch startup behavior makes that attempt inconclusive for WGC.

| Requirement/check | Result |
| --- | --- |
| AC03 cursor-free full-window PNG | Attempted; blocked before first image. Cursor disabling is implemented, pixel comparison untested. |
| AC03 occluded-window freshness | Untested: first capture prerequisite failed. Test would repaint red to lime behind a blue cover and decode PNG center pixels. |
| AC04 selected visible pixels | Untested: first capture prerequisite failed. Test expects blue covering-window pixels. |
| AC04 starting-monitor clipping | Untested native behavior: assertion for a 50x50 selection clipped to 10x10 is present but not reached. |
| AC07 UI exclusion | Untested: affinity and decoded-pixel assertion are present but not reached. No exclusion guarantee established. |
| AC14 ordinary failure/batch retention | Probe catches failure and exits normally with code 1, recording the exception. Retained-PNG/recovery assertions were not reached; batch retention remains unproven. No production batch is touched. |
| Multi-monitor / mixed DPI | Deferred, untested; not completion prerequisites. |

The exact required Build.ps1 command exits 1 with empty captured output. A supplemental serial app build succeeds with zero warnings/errors (exit 0). Core and Windows settings harnesses independently pass (exit 0). These supplemental checks do not convert the required build failure or native acceptance failure into success. Commands, captured output and exit codes are stored in the run directory.

Stop the screenshot-dependent implementation chain until a supported native run proves approved content semantics. Do not substitute visible screen pixels for full-window captures. No passing green test or completed refactor is claimed. The added source/probe are an unvalidated bounded attempt requiring follow-up.

Large batches retain full PNG contents in memory; configurable limits remain deferred. This task adds no clipboard disk spill and cannot guarantee recovery from OS/process memory exhaustion.

## Parent native diagnostic recovery (2026-10-14)
The same acceptance harness was run with local permissions:
`dotnet run --project docs/factory/runs/20261014-005733-564/Acceptance.csproj --no-restore -p:BuildInParallel=false -p:UseSharedCompilation=false -- docs/factory/runs/20261014-005733-564/local-diagnostic`
Exit code: 0. Evidence: docs/factory/runs/20261014-005733-564/local-diagnostic/result.txt and corresponding fixture PNGs.
Passed: full-window, occluded-fresh-window, visible-rectangle, starting-monitor clipping, excluded-ui, invalid-target recovery and retention of previous image bytes.
The unchanged native probe fails under restricted execution but passes with local permissions; this establishes environment-dependent behavior without identifying the exact denied operation. Cursor pixel comparison remains untested, as do the approved deferred multi-monitor/mixed-DPI checks. T01 still requires mandatory runner checks and independent review; this diagnostic is not overall task approval.

T01 gate closure: exact local Debug and Release Build.ps1 both returned exit 0. Code re-review found no remaining code blocker; current evidence summary reconciled. T01 complete for the approved single-monitor scope; deferred checks remain untested.
