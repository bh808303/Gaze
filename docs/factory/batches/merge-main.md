# Merge live Transcribe batches into main

Human explicitly requested merging the feature into main. Merge parents: main 7adfba9 (Gaze 0.6alpha installer preparation) and codex/transcribe-live-batches 729c0f2. Existing untracked files in the main checkout were preserved and excluded from the merge commit.

## Conflict resolution and review
Preserve main's compact tray menu, header power icon, release links/version, manual DPI-aware Transcribe placement, tray-click restoration and white-on-red ZIP button hover/press style. Integrate live batches, limits, retry, temporary cleanup and confirmed MP4/PNG/ZIP Clear. X/Stop close without archive; ZIP exports explicitly. Combine constructor options for placement and limit snapshot rather than dropping either branch's behavior. Update the existing UI validation to assert restoration does not end capture and X and ZIP dispatch distinct operations. Keep prior release/installer work intact. Remove obsolete Done/Cancel window-control documentation and preserve the compact-navigation instructions.

Reviewed the merge resolutions and resulting staged diff for scope, lifecycle, ownership, UI callback wiring and preservation of newer main behavior. No unresolved conflicts or actionable blocker found. Original feature slices had already passed independent Factory reviews; this record covers the merge integration.

## Validation
- pwsh -NoProfile -File scripts/Build.ps1 -Release: passed, including core tests. BuildInParallel=false environment used.
- dotnet build src/Gaze.App --no-restore -c Debug -p:Platform=x64 -p:BuildInParallel=false -m:1 -p:UseSharedCompilation=false -o artifacts/merge-debug: passed.
- dotnet run --project tests/Gaze.Settings.Tests -p:BuildInParallel=false -m:1: passed.
- dotnet run --project tests/Gaze.Clipboard.Tests -p:BuildInParallel=false -m:1: passed; real clipboard sequence unchanged. Local permissions used for junction fixtures.
- Python 3.14 -m unittest discover -s tests -p test_timeline_reader.py: all five passed.
- dotnet artifacts/app/Gaze.App.dll --transcribe-ui-check docs/factory/runs/merge-live-batches/ui-check.txt: passed restore, minimize/restore, X-close and explicit ZIP dispatch.
- dotnet artifacts/app/Gaze.App.dll --transcribe-batch-controller-probe docs/factory/runs/merge-live-batches/controller: passed all injected lifecycle, errors, retry, stale capture, limits, current ZIP and real WPF Closing checks.
- git diff --cached --check: passed.

Raw targeted logs/rendered panel evidence: docs/factory/runs/merge-live-batches (local ignored evidence). NuGet vulnerability lookup emitted NU1900 network-access warnings; compilation succeeded. Existing nullable warnings occur in core test assertions. Initial settings no-restore attempt lacked assets; normal restore/run then passed. Python launcher alias was inaccessible in this execution context; installed Python executable used directly.

Actual Claude paste/ingestion, physical capture input/pixels, mixed DPI/multiple monitors and actual video recording remain unverified here, consistent with feature validation limits. Native/injected tests did not upload to Claude or recycle user files. No installer rebuilt or release published by this merge.
