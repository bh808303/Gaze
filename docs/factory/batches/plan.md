# Plan: live Transcribe batches

Approved design and build instruction: see alignment.md. This is an extension; historical incomplete Factory plan/manifests are preserved and not marked complete.

1. B01: current-batch model, aggregate bytes, limits, Next Batch reset and archive boundary behavior. Core + focused behavioral tests. AC02/03/07.
2. B02: staged PNG clipboard publication, ownership-aware clear, retry/current snapshot, temporary artifact lifecycle. Inject native boundaries for behavioral tests without touching real user clipboard. AC01/03/04/06/09.
3. B03: integrate topmost panel/controller/host Start/Stop/ZIP semantics and feedback. Extend native fixture for revised UI lifecycle rather than retaining obsolete close prompts. AC01-08/10. This shared controller/window/host contract spans more than five files; keep changes limited to this one integrated feature path.
4. B04: rename setting/helper text in both panels, preserve persistence/video, regression tests and user documentation. AC07/08/10.

Dependencies B01 -> B02 -> B03 -> B04. Each uses fresh Factory TDD worker, mandatory checks, fresh independent reviewer, then local commit before next worker. Runner manifest: tasks.json in this directory. No factory launcher changes needed. All evidence under docs/factory/runs. Final combined verification and diff review after all tasks. No merge, deployment or publishing.

Risks: clipboard busy/overwritten; files outlive UI; Next Batch/Undo invalidate pending retry; cleanup unsafe around redirects/live instances; 29MB ZIP overhead; available desktop automation/Claude paste tests. Mitigate with injected failures and real native fixture where possible; record unavailable checks honestly.

Mandatory checks: Debug build + core, Release build + core, settings executable tests, Python timeline tests, whitespace check. No baseline weakening. Use local cached dependencies on restore trouble and diagnose actual failure before escalating.

## Completion record
All four implementation slices passed fresh independent Factory review: B01 accepted retry `20261025-121503-382`, B02 `20261025-122052-947`, B03 `20261025-123008-757`, B04 `20261025-125010-087`. Original B01 attempt remains unaccepted and preserved. Mandatory Debug/Release, core, settings, clipboard and Python checks passed. Current task manifest is exhausted; create a new approved task before invoking runner again. Final verification: verification.md. Parent review/handoff: review.md. Runtime compatibility gaps remain explicit and are not claimed passed.

The optional extension of configured-folder Clear to PNG/ZIP was raised after discovering its current MP4-only filter. No answer received during this run; preserve existing Clear behavior rather than broaden deletion scope. Automatic temporary Transcribe cleanup preserves all configured saved files.

Follow-up: human has now answered "Include PNG and ZIP alongside MP4, with confirmation". Prior pending notes are superseded. Factory task B05 implements AC11; see clear-saved-files.md. Existing B01-B04 work and checks remain accepted.

## B05 supersedes pending Clear choice
Human approved PNG and ZIP alongside MP4, with confirmation. Implemented exact case-insensitive top-level extension selection, default-No count/types/folder confirmation and injected recycling tests. Any earlier MP4-only/pending-choice statement in this file is historical and superseded. Automatic temp cleanup and gallery/video behavior are unchanged. See B05 verification and raw evidence in docs/factory/runs/20261025-134958-805. Independent B05 review remains a separate gate.

B05 completed and independently reviewed: `20261025-134958-805`. All mandatory checks passed. Saved-folder Clear choice resolved and implemented; task manifest exhausted again.
