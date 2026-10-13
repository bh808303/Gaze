# Factory task: Transcribe capture mode
## Outcome
Add Transcribe to Gaze's existing capture dropdown to collect clean screenshots for later agent transcription, without recording MP4.
## Confirmed acceptance criteria
- Existing Start begins a screenshot session and opens a separate movable window matching the main panel size, logo, margins and style; it is not always on top.
- Middle-click captures the window under the pointer, with no locked target. Middle-button drag displays a rectangle and release captures that screen area, including across windows. Consume capture middle-button gestures, preserving ordinary left-click, double-click, drag, keyboard and scrolling controls.
- Rectangle selection across monitors is not required; confine the initial implementation to the starting monitor. Do not interpret this as prohibiting window captures on other monitors.
- Lightweight window shows count, latest thumbnail, Undo last capture, Done and Cancel. Keep full gallery expansion for later.
- Capture feedback: brief border flash and updated count/thumbnail; no sound by default. Exclude capture UI/selection/feedback from resulting images where practical; document limitations.
- Screenshot PNG filenames contain capture sequence and window title at capture time, sanitizing invalid filename characters. Rectangle uses the starting window title.
- Stop and Done have identical export behavior. With no captures, close without ZIP creation or clipboard mutation.
- With captures, closing the session window offers Finish / Discard / Keep capturing. Cancel discards the session.
- Done/Stop exports ordered PNGs plus instructions.txt in ZIP. Instructions tell the agent to transcribe in order, merge overlaps, remove duplicates, preserve headings and flag unreadable text without guessing.
- Existing clipboard setting enabled: construct ZIP in memory and offer virtual ZIP file on clipboard, no ZIP or screenshot disk save. Destination support must be tested and documented; no promise of universal paste compatibility.
- Clipboard setting disabled: automatically save ZIP in configured Gaze video folder.
- Export failure keeps batch in memory, shows Retry, and never silently discards or falls back from clipboard-only to disk.
## Constraints
Windows desktop Gaze; inspect current code before selecting implementation. No implementation until specification and plan gates are satisfied. Preserve unrelated user changes and existing video modes.
## Validation commands
Inspect scripts/Build.ps1; existing project commands include dotnet run --project tests/Gaze.Tests and dotnet run --project tests/Gaze.Settings.Tests. Derive actual mandatory checks from current repository. Native gesture, DPI, screenshot and clipboard compatibility need meaningful desktop validation; report untested paths accurately.
## Delivery
Local feature work only. No merge, deployment or publishing authorized. Keep code and evidence in Gaze repository.
## Existing factory records
Existing docs/factory/spec.md and tasks.json belong to an earlier feature. Preserve them before replacing active records, using docs/factory/previous-timeline-metadata/ if needed; do not mix tasks.
## Alignment provenance
Human agreed each rule in the current chat and then requested "startup the factory !". This confirms the captured feature direction and authorizes starting the factory, not automatic implementation or approval of an unwritten specification/plan.
## Open technical questions
Investigate gesture threshold, capture API and actual receiving-app support from source and validation. Clipboard lifetime, memory policy and specialized failure handling are settled by the follow-up decisions below; do not reopen deferred scope without a material scope change. Record newly discovered uncertainty in docs/factory/spec.md. No special cross-monitor rectangle support required.

## Follow-up decisions confirmed in the parent chat (2026-10-14)
- Clipboard lifetime: user accepts clipboard-only batch becoming unavailable if Gaze exits before paste. No persistence to disk or special exit-confirmation requirement for this case; document limitation.
- Memory policy: user said "sort taht later". Defer configurable memory/batch limits; do not expand initial scope with a memory-policy interview. Document large-batch risk; no disk spill for clipboard-only mode.
- Invalid area: user said "just disregard the selection". Ignore selections with no usable area; no capture and no count increment.
- Other capture failure cases: user said "the rest are to much of an edge case". Defer specialized handling for protected/closed windows and resource failures; ordinary error containment must preserve the process and existing batch, without adding dedicated edge-case flows.
- The user requested "ok start up the factory again?" after these decisions. Resume specification preparation with earlier confirmed capture/export requirements, do not repeat their confirmation questions. Specification and plan approvals remain future gates.

## Human specification review decisions
Received directly in the parent chat on 2026-10-14:
- Capture content: user agreed to cursor-free full-window content even when partly covered. Rectangle captures remain selected visible screen pixels.
- Quitting with unexported captures: user approved Finish / Discard / Keep capturing. Keep capturing cancels quit; a failed Finish export retains the batch and does not complete quit; Discard permits quit.
- Primary intended clipboard destination: Claude Desktop. Validate actual virtual-ZIP paste/attachment and ZIP ingestion separately; API clipboard success is insufficient. Record tested Claude Desktop/OS versions and actual results. If unsupported, report the incompatibility and reopen export design with the user; no unauthorized disk fallback.
These answers resolve the specified review questions, but do not constitute explicit approval of the entire specification or the previously proposed Start/shortcut details.
