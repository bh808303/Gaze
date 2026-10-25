# Live-batch final verification — B04 (2026-10-25)

Implementation and automated verification passed for this bounded task. This is not acceptance of actual Claude paste/ingestion, physical desktop gestures, or tray/video behavior. No Claude upload/messages, commit, merge or deployment occurred. Factory manifests and validation scripts are unchanged.

## Behavioral RED and GREEN

Evidence root: `docs/factory/runs/20261025-125010-087`. Each named log is an individual file with the command, actual output and exit code; commands ran from repository root.

| Check | Evidence file under the run root | Exit / result |
| --- | --- | --- |
| Settings preimplementation RED | settings-red.txt | 1: compiled; actual WinForms label/accessibility/persisted-value assertion failed because the old label remained |
| ZIP Retry preimplementation RED | retry-red.txt | 1: compiled; ZIP Retry assertion failed because Retry published PNGs and left the session open instead of retrying ZIP and closing after success |
| Settings initial GREEN | Gaze.Settings.Tests-green.txt | 0 |
| ZIP Retry initial GREEN | Gaze.Clipboard.Tests-green.txt | 0 |
| Final settings including actual Save callbacks | settings-complete.txt | 0 |
| Final clipboard/coordinator suite | clipboard-final.txt | 0 |
| Mandatory Debug script including existing core/video tests | debug-complete.txt | 0; zero build warnings/errors |
| Mandatory Release script including existing core/video tests | release-complete.txt | 0; zero build warnings/errors |
| Mandatory Python timeline tests | python.txt | 0; all 5 tests passed |
| Mandatory whitespace check | diff-check.txt | 0 |
| Actual WPF settings save/reload/Cancel/accessibility probe + render | settings-render-final.txt | 0 |
| Final controller/panel probe | controller-complete.txt and panel-complete/result.txt | 0; injected capture and clipboard boundaries |

Both required REDs ran before production changes and failed intended assertions, not missing APIs. Settings coverage checks legacy JSON `SplitAt29Mb`, independent AutoCopy=false, the real WinForms label/helper/accessibility, Save toggling false/true and JSON round-trips, plus WPF XAML label/helper/accessibility. The actual WPF splitting probe additionally checks true/false/true Save/reload, Cancel and accessible name. Existing AutoCopy/video tests were left unchanged.

Coordinator regressions cover repeated ZIP failure, successful ZIP Retry closing only after archive publication, and capture/Undo/Next Batch superseding ZIP retry intent. Additional assertions cover count and byte warnings with actual Claude/standard limits and Next Batch/ZIP guidance. Existing ownership, overwrite, current-batch ZIP, ZIP overhead, staging failures, cleanup/junction/active-instance and lifetime checks pass; clipboard harness reports the real clipboard sequence unchanged. Current-state publication remains guarded against reentry and stale capture generation.

No additional abstraction was needed: one retry-intent flag extends the existing coordinator. Size formatting is confined to the panel; encoded-byte accounting and limit enforcement remain unchanged. Both unchanged Build.ps1 commands ran with environment `BuildInParallel=false`, using parent-provided Python PATH; no mandatory command or script was weakened. Earlier build logs are retained because later panel/probe edits required the final rebuilds.

## Render inspection and feasible native evidence

- `settings.png`: visually inspected final WPF settings; full Claude Desktop Clipboard label, toggle, concise video/Transcribe helper and Save/Cancel are readable. The first render exposed a clipped label (`settings-clipped-before-fix.png`); spanning the row with space reserved for the toggle fixed it. `settings.png.state.json` records persisted state after actual probe toggling.
- `panel-final/limit-panel.png`: visually inspected current session layout; 20 captures, readable KB units, actual 20 images / 29 MB warning and Next Batch/ZIP guidance, plus all action buttons fit. Warning survives controller refresh.
- `panel-complete/zip-retry-panel.png`: visually inspected final failed-ZIP panel; count and persistent clipboard error remain readable and Retry is visible. The corresponding final probe proves repeated failed Retry retains the real panel/batch, then successful Retry publishes a ZIP and closes.
- One repeated final fixture render, `panel-complete/limit-panel.png`, was entirely blank even though all controller status assertions passed. The earlier same-layout `panel-final/limit-panel.png` is readable, and the later ZIP-error render is readable. This is an unresolved intermittent render-evidence limitation, not evidence of universal display correctness. Investigate render/composition timing and inspect the real panel before claiming reliable desktop rendering across environments.

The first executable apphost launches returned asynchronously without a usable exit code (`settings-render.txt`, `controller-render.txt`); their blank EXIT fields are not passes. Subsequent `dotnet artifacts/app/Gaze.App.dll ...` invocations supplied the recorded exit codes. An intermediate settings failure revealed Row overwriting the accessible name with File size; the final code restores the requested accessible name after Row creation (`settings-intermediate-failure.txt`).

## B01 history

The original B01 RED failed on absent constructor/NextBatch APIs through runtime lookup. It was not a behavioral RED and was not accepted; the earlier supplementary postimplementation scaffold checks did not repair that timing evidence. The recovery retry `20261025-121503-382` began with a committed compiling API contract: unchanged tests failed at runtime for count/byte enforcement and reset state before implementation, then passed. `build.md` now states this distinction accurately; original evidence is preserved.

## Remaining checks and actionable risks

- Current multi-PNG Claude Desktop paste and ZIP ingestion are untested. The historical 2026-10-14 standalone ZIP probe is not current integrated acceptance. A human must separately paste each route and inspect attachment order/contents in the intended installed version before claiming compatibility.
- Physical middle-click/drag consumption, ordinary input preservation, actual screenshot pixels/exclusion, native tray shortcuts/Stop/quit and representative video capture remain untested here. B03's controller probe success is retained; unavailable mouse injection was not repeated. Validate on an interactive desktop with suitable native input access.
- Multiple monitors and mixed DPI remain deferred/untested. Render inspection is limited to the available fixture scale, including the intermittent blank render noted above.
- Encoded PNG limits do not bound peak RAM; ZIP construction can duplicate data, and archive overhead can reject an otherwise valid 29 MB PNG batch. Split smaller batches and test realistic workloads before assuming memory capacity.
- Staged files survive panel closure until app exit, and no age/storage quota exists. Long-running capture use can grow temporary storage. Exit intentionally invalidates temporary clipboard paths; finish destination reads first. Cleanup failures can leave recognized artifacts for later startup; monitor/report cleanup errors rather than deleting unrelated paths manually.

README, docs/transcribe.md and CHANGELOG now describe the final Start/capture/paste/Next Batch/ZIP/X flow, both limit presets, persistent rejection and retry, clipboard overwrite, staged lifetime and startup/exit cleanup. Historical changelog entries remain historical.

## Parent completion
All four Factory independent reviews passed; see review.md for run IDs, final scope review and remaining runtime gaps. Supplementary ZIP-success mutation was caught by tests; exact source restored and full clipboard suite passed locally (parent-mutation-green-local.txt, exit 0, real clipboard sequence unchanged). Local output includes the adjacent Gaze build's existing FFmpeg dependency; source unchanged. Configured-folder Clear remains MP4-only pending the optional extension choice; temporary cleanup preserves saved files.
