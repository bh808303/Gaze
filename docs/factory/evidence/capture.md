# T01 native screenshot feasibility — blocked

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
