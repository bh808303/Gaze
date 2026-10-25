# Plan: live Transcribe batches

Approved design and build instruction: see alignment.md. This is an extension; historical incomplete Factory plan/manifests are preserved and not marked complete.

1. B01: current-batch model, aggregate bytes, limits, Next Batch reset and archive boundary behavior. Core + focused behavioral tests. AC02/03/07.
2. B02: staged PNG clipboard publication, ownership-aware clear, retry/current snapshot, temporary artifact lifecycle. Inject native boundaries for behavioral tests without touching real user clipboard. AC01/03/04/06/09.
3. B03: integrate topmost panel/controller/host Start/Stop/ZIP semantics and feedback. Extend native fixture for revised UI lifecycle rather than retaining obsolete close prompts. AC01-08/10. This shared controller/window/host contract spans more than five files; keep changes limited to this one integrated feature path.
4. B04: rename setting/helper text in both panels, preserve persistence/video, regression tests and user documentation. AC07/08/10.

Dependencies B01 -> B02 -> B03 -> B04. Each uses fresh Factory TDD worker, mandatory checks, fresh independent reviewer, then local commit before next worker. Runner manifest: tasks.json in this directory. No factory launcher changes needed. All evidence under docs/factory/runs. Final combined verification and diff review after all tasks. No merge, deployment or publishing.

Risks: clipboard busy/overwritten; files outlive UI; Next Batch/Undo invalidate pending retry; cleanup unsafe around redirects/live instances; 29MB ZIP overhead; available desktop automation/Claude paste tests. Mitigate with injected failures and real native fixture where possible; record unavailable checks honestly.

Mandatory checks: Debug build + core, Release build + core, settings executable tests, Python timeline tests, whitespace check. No baseline weakening. Use local cached dependencies on restore trouble and diagnose actual failure before escalating.
