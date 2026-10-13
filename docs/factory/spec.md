# Spec: Transcribe capture mode

Status: draft for human scope review, 2026-10-14. Alignment is confirmed through the trusted parent-session handoff recorded in alignment.md. Specification and plan are not approved. This task authorizes specification only.

## Objective and scope
Add Transcribe to Gaze's capture dropdown so users can collect screenshots of changing windows or screen areas and export an ordered ZIP for later agent transcription. Do not record MP4 or perform OCR. This is one end-to-end capability, with capture, session controls and export contributing to one outcome. Preserve existing video modes and unrelated changes.

Assumption for review: one capture session at a time, consistent with the existing application. Native API selection belongs to the later plan. Newly discovered uncertainties below are not silently confirmed decisions.

## Repository facts and structure
- src/Gaze.Core/Session.cs: current numeric modes Window=0, AllMonitors=1, CurrentMonitor=2; preserve serialized values.
- src/Gaze.Panel/PanelWindow.xaml and .xaml.cs: actual WPF panel is 332.35 x 540 DIP, contains dropdown and recording action labels. Video currently arms before starting.
- src/Gaze.App/Settings.cs: AutoCopy defaults true; Folder defaults to Videos/Gaze; another WinForms mode list exists.
- src/Gaze.App/Capture.cs: clipboard uses SetFileDropList with disk paths, not virtual in-memory files.
- src/Gaze.App/InputEvents.cs: global mouse observer forwards all events; gesture consumption is new behavior.
- src/Gaze.App/GraphicsSource.cs: asynchronous Windows Graphics Capture/D3D11 frames, currently with cursor enabled; not already a verified screenshot path.
- src/Gaze.App/Native.cs: pointer/window/monitor/DPI/title and capture-affinity primitives. Existing video target restrictions are not automatically Transcribe policy.
- src/Gaze.App/Overlay.cs: topmost capture-excluded overlay, unsuitable as the non-topmost session window.
- tests/Gaze.Tests: executable core/linked-source regression harness. tests/Gaze.Settings.Tests: separate Windows settings harness.
- docs/ and README.md: user documentation. docs/factory/: scope and later evidence. Previous timeline spec/tasks are preserved in previous-timeline-metadata/; no active Transcribe task list is created.

Stack: C#/.NET 10, Windows desktop x64, WinForms host and WPF panel. App targets Windows 10 build 19041; global.json selects SDK 10.0.303. Manifest declares PerMonitorV2 awareness. These declarations do not establish runtime validation across all hardware.

## Acceptance criteria and decision mapping
| ID | Decision | Measurable criterion | Verification |
|---|---|---|---|
| AC01 | D1 | Dropdown includes Transcribe; existing numeric mode values remain compatible. Start opens a screenshot session without locking a target or creating MP4. | Settings round-trip; desktop start; video regression. |
| AC02 | D1 | Separate movable non-topmost window matches main panel size, logo, margins and style; shows count, latest thumbnail, Undo, Done and Cancel; no full gallery. | DPI visual comparison, movement and z-order check. |
| AC03 | D1 | Middle-click captures the window under the pointer for each gesture, including a different window next time and windows on other monitors. | Two-window and multi-monitor scenarios. |
| AC04 | D1 | Middle-drag displays a rectangle; release captures selected screen pixels across windows, confined to the starting monitor. | Geometry tests and desktop pixel comparison. |
| AC05 | D1 | Capture middle-button gestures are consumed during session; ordinary left-click, double-click, left-drag, keyboard and wheel scrolling work. Capture interception ends with session. | Target application input checks and lifecycle tests. |
| AC06 | D1,D5 | Each successful capture increments count once and updates thumbnail. Undo removes only latest retained capture and updates both; empty Undo is harmless. Unusable selection adds no image/count. | Batch and geometry tests; UI checks. |
| AC07 | D1 | Brief border flash signals capture; no sound by default. Exclude capture UI/selection/feedback where practical; document observed limitations. | Inspect screenshots while UI/overlays are present. |
| AC08 | D1,D2 | ZIP has one valid PNG per retained capture plus instructions.txt. PNG names have sortable capture sequence and sanitized title captured at gesture time; rectangle uses starting window title. Valid unique names for repeated, empty, long or invalid titles. | Decode PNGs; inspect ZIP order/names; Undo and sanitization tests. |
| AC09 | D2 | Instructions specify ordered transcription, overlap merging, deduplication, heading preservation and unreadable text flagged without guessing. | Content assertions and review. |
| AC10 | D2 | Done and Stop share export behavior. Empty session closes without ZIP or clipboard mutation. Nonempty close offers Finish / Discard / Keep capturing. Cancel discards without export. | State tests and native dialog checks. |
| AC11 | D2,D3,D4 | AutoCopy on constructs ZIP in memory and offers virtual ZIP on clipboard; no screenshot/ZIP/temp spill writes. Document tested destinations; no universal paste guarantee. Clipboard may become unavailable at Gaze exit; no persistence or special exit prompt required. | Observe file writes; paste and decode ZIP in named apps; process-exit check. |
| AC12 | D2 | AutoCopy off automatically saves ZIP to configured video folder, without silently overwriting unrelated existing files. | Folder, collision and ZIP tests. |
| AC13 | D2 | Export failure retains batch in memory, shows Retry, and never silently discards or falls back from clipboard to disk. Retry exports retained ordered contents. | Inject clipboard/folder failures; inspect retained batch and retry output. |
| AC14 | D4,D6 | Defer configurable memory limits and specialized protected/closed-window/resource workflows. Ordinary capture errors preserve process and previous batch, with no false count increment. Document large-batch memory risk and no clipboard disk spill. | Recoverable failure injection and documentation review; no guarantee for unrecoverable OS/process exhaustion. |
| AC15 | D1,D2 | Existing video modes/settings/export remain functional. Document gestures, routes, clipboard lifetime, tested destinations and capture limits. | Existing harnesses and representative video desktop regression. |

Exact sequence formatting, flash duration and API selection are technical choices for later planning; do not represent them as confirmed human decisions.

## Commands
Run from repository root during later implementation validation:

```powershell
pwsh -NoProfile -File scripts/Build.ps1
pwsh -NoProfile -File scripts/Build.ps1 -Release
dotnet run --project tests/Gaze.Tests
dotnet run --project tests/Gaze.Settings.Tests
```

Build.ps1 restores into .packages, builds x64 into artifacts/app, then runs Gaze.Tests. Release is available for release validation. Avoid redundant core-test reruns after the script unless justified. Settings tests are separate. No lint command or numerical coverage gate was identified. These commands were inspected, not run for this documentation-only task.

## Code style
Follow file-scoped namespaces, PascalCase types/public members, camelCase locals and nullable analysis. Keep batch/order/geometry/ZIP logic independently testable from native capture and clipboard lifetime. Existing style from Session.cs:

```csharp
public void Tick(TimeSpan elapsed)
{
    if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
    if (State == SessionState.Recording) Duration += elapsed;
}
```

This is a style reference, not implementation.

## Testing strategy
Use existing executable harnesses for state transitions, gesture classification, clipping, Undo/order, filename sanitization, ZIP contents, export retry and settings compatibility. No new framework or coverage threshold is prescribed.

Native Windows checks must cover gesture consumption and other input preservation, clean screenshot pixels, mixed DPI/multiple monitors, non-topmost window and virtual-file clipboard interoperability. Clipboard API success alone does not establish receiver support: paste and decode the expected ZIP. Record OS/app versions, scenarios and outcomes inside this repository; mark unavailable desktop scenarios untested. Specialized capture cases remain deferred.

## Boundaries
- Always: preserve existing behavior/unrelated changes, keep evidence here, retain export-failure batches, trace evidence to criteria, report untested paths, reopen affected alignment decisions when scope changes.
- Ask first: scope changes, new dependencies/platform requirements or persistence policy. Obtain human spec approval before planning and subsequent gate approval before implementation.
- Never: implement in this task, approve spec/plan for the human, reuse timeline tasks, silently spill clipboard exports or discard failed batches, promise universal clipboard compatibility, or claim unperformed native validation. No merge/deploy/publishing authorization.

## Newly discovered uncertainties
These review items are not adopted requirements or scope additions.
- U1 (D1): Current video action arms before Start. Transcribe requires Start without target lock. Exact action labels and arming integration need resolution without accidentally inheriting target selection.
- U2 (D1): Cursor inclusion and occluded-window semantics (full window content versus visible screen pixels) are unspecified. Recommendation for review: clean cursor-free window content, subject to feasibility and human decision.
- U3 (D1/D2): Existing shortcuts/double-Esc, middle gestures over Gaze controls, and quitting while an active or failed-export batch exists need integration semantics. Accepted post-export clipboard lifetime does not settle unsaved-session quit behavior.
- U4 (D1): Drag threshold, capture API/frame freshness and overlay exclusion need technical investigation and native evidence. Select and document platform-consistent classification; do not invent a human-approved threshold.
- U5 (D2/D3): No virtual-file implementation or destination matrix exists. Verify clipboard ownership after session-window close/replacement and record actual tested apps. Intended receiving apps are not named; do not promise unspecified compatibility.

Explicitly deferred: configurable memory/batch limits, special protected/closed-window/resource handling, full gallery, cross-monitor rectangles and clipboard persistence after Gaze exit. Do not reopen those interviews without material scope change.

## Human review gate
Approve specification scope or request changes, including the newly discovered uncertainties. No approval is recorded. Stop after specification delivery: no plan, task list or implementation in this turn.

## Human specification review decisions
Received directly in the parent chat on 2026-10-14:
- Capture content: user agreed to cursor-free full-window content even when partly covered. Rectangle captures remain selected visible screen pixels.
- Quitting with unexported captures: user approved Finish / Discard / Keep capturing. Keep capturing cancels quit; a failed Finish export retains the batch and does not complete quit; Discard permits quit.
- Primary intended clipboard destination: Claude Desktop. Validate actual virtual-ZIP paste/attachment and ZIP ingestion separately; API clipboard success is insufficient. Record tested Claude Desktop/OS versions and actual results. If unsupported, report the incompatibility and reopen export design with the user; no unauthorized disk fallback.
These answers resolve the specified review questions, but do not constitute explicit approval of the entire specification or the previously proposed Start/shortcut details.

## Specification scope approval
On 2026-10-14, after reviewing the specification and recording the capture-content, quit behavior and Claude Desktop decisions, the human replied exactly: "approved" to the request to approve specification scope and move the factory to planning.
Specification scope is approved, including the appended review decisions. Planning is authorized. This is not plan approval, implementation approval, merge or deployment authorization. Previously labeled uncertainties are resolved by these review decisions where applicable; remaining technical choices belong in the plan, and any material behavior change must be surfaced.

## Human-approved validation adjustment
On 2026-10-14 the human replied "agreed" to continuing single-monitor implementation/testing while deferring unavailable multi-monitor and mixed-DPI validation. Those unavailable checks and denied hardware/driver metadata queries are not prerequisites for implementation or completion of the current task. Run all feasible single-monitor behavioral capture checks and required automated checks; retain approved capture behavior, record unavailable scenarios as deferred/untested, and do not claim complete multi-monitor validation. Evidence records supersede the earlier T01 prerequisite-blocking interpretation. No weakening of feasible tests or implementation behavior is authorized.
