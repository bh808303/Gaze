# T01 native screenshot feasibility

Status: **blocked by validation prerequisites** (2026-10-14). No screenshot implementation or production controls were changed. This is not evidence that Windows Graphics Capture fails the approved semantics.

## Environment observed

- Windows runtime reports 10.0.26300; x64.
- .NET SDK 10.0.303; runtime 10.0.11.
- One attached display: `\\.\DISPLAY1`, physical bounds `(0,0,3072,1728)`.
- Hardware model, GPU and driver version: unavailable; CIM queries returned Access denied. No hardware/version claims are inferred.
- Mixed-DPI monitor pair: unavailable. DPI values were not measured.

## Required observations

| Scenario | Result |
|---|---|
| AC03 cursor-free full-window PNG and changing target | Untested |
| AC03 occluded-window freshness | Untested |
| AC04 visible-pixel rectangle and starting-monitor clipping | Untested |
| AC03/AC04 other-monitor and mixed-DPI capture | Blocked: only one display is attached |
| AC07 capture UI / selection / feedback exclusion | Untested |
| AC14 ordinary failure preserves process and previous batch | Untested |

The existing `GraphicsSource` enables cursor capture and publishes an asynchronous latest frame. Inspection does not prove cursor-free screenshot semantics, freshness, or exclusion. No acceptance RED/GREEN cycle was started; the prerequisite failure is not a behavioral red test. Build was not run because implementation stopped before changes.

## Dependency limitation

Keep the affected screenshot integration dependency chain stopped until native feasibility can be tested with a mixed-DPI multi-monitor desktop and OS/GPU/driver versions can be recorded. Approved full-window and visible-screen semantics remain unchanged; no substitute semantics are approved by this result.

Exact prerequisite commands, outputs and exit codes are saved in `docs/factory/runs/20261014-004550-136/`. `desktop-prerequisite.ps1` exits 1 for the missing monitor prerequisite. `prerequisites.output.txt` also records the denied hardware queries; its overall shell status is not a capture acceptance test.
