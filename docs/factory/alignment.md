# Transcribe mode alignment
## Confirmation status
Confirmed through an explicit trusted parent-session handoff received from the human on 2026-10-14. The handoff states that Q1 and Q2 were explicitly confirmed in the parent chat and authorizes specification preparation with the follow-up decisions below. This supersedes the earlier pending status; no repeated Q1/Q2 interview is needed. The original parent transcript is not reproduced here. Specification scope and plan approval remain pending; implementation is outside this task.

## Confirmed shared understanding
- Add Transcribe screenshot sessions to the existing capture dropdown; do not record MP4.
- Start opens a separate movable, non-topmost window styled like the main panel, with count, latest thumbnail, Undo, Done, and Cancel; no full gallery.
- Middle-click captures the window under the pointer. Middle-drag captures a rectangle confined to the starting monitor. Consume those gestures while preserving other normal input. Window capture on other monitors remains allowed.
- Give border-flash feedback without sound; exclude capture UI where practical and document limitations.
- Export ordered PNGs and instructions.txt in a ZIP; filenames include sequence and sanitized window title (rectangle uses its starting window title). Instructions cover ordered transcription, overlap merging, deduplication, headings, and unreadable text.
- Existing clipboard setting selects an in-memory virtual ZIP clipboard export without image/ZIP disk writes; otherwise save to the configured video folder. Document tested receiving applications and compatibility limits.
- Done and Stop behave identically. Empty sessions close without ZIP or clipboard changes. Closing a nonempty session offers Finish / Discard / Keep capturing. Cancel discards. Export failures retain the batch and offer Retry, without automatic disk fallback.

## Decision tree and unresolved questions
- D1: Screenshot-only outcome and capture interaction confirmed in the parent chat, as explicitly relayed by the human.
- D2: Export, session lifecycle, and failure retention confirmed in the parent chat, as explicitly relayed by the human.
- D3: Clipboard availability only until Gaze exits accepted; no persistence or special exit prompt.
- D4: Configurable memory/batch limits deferred; no clipboard-mode disk spill.
- D5: Unusable selection ignored without count increment.
- D6: Specialized capture edge cases deferred; contain ordinary errors and preserve existing batch.
- Newly discovered uncertainties U1–U5 are recorded in spec.md for human review. They are not silently confirmed decisions. Reopen the affected D1/D2 decisions if resolving them changes scope.

## Repository facts and preservation
- Root AGENTS.md is absent. The ancestor Software Factory/AGENTS.md applies: keep target application code and run evidence in this repository; use skills on demand through native discovery.
- Existing docs/factory/spec.md concerns optional timeline metadata. Preserve it and related tasks before replacing active records after alignment confirmation.
- Repository investigation completed by a read-only delegated agent and direct source reads. Current WPF panel is 332.35 × 540 DIP; input hooks forward all events; screenshot primitives use asynchronous Windows Graphics Capture; existing clipboard support uses disk file paths, not virtual files. See spec.md for references and validation requirements.
- Previous timeline spec and tasks are preserved in previous-timeline-metadata/. No new plan or task list has been created.

## Approval gates
1. Record interview answers and resolve the dependency-aware decision frontier.
2. Summarize shared understanding and wait for explicit human confirmation before writing the Transcribe specification.
3. Complete specification only, then request scope approval. Reopen affected alignment decisions if scope changes.

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

## Specification scope approval
On 2026-10-14, after reviewing the specification and recording the capture-content, quit behavior and Claude Desktop decisions, the human replied exactly: "approved" to the request to approve specification scope and move the factory to planning.
Specification scope is approved, including the appended review decisions. Planning is authorized. This is not plan approval, implementation approval, merge or deployment authorization. Previously labeled uncertainties are resolved by these review decisions where applicable; remaining technical choices belong in the plan, and any material behavior change must be surfaced.
