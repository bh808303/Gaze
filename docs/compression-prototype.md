# Original long-GOP compression prototype

Question: does CRF18 / veryfast / a maximum 10-second keyframe interval retain Original text quality while saving space?

Throwaway branch: codex/prototype-original-long-gop. Verdict pending real recording comparison. Not for main or release.

Quit any running Gaze first. Run `artifacts/prototype-long-gop/Start-Prototype.ps1`.
The flag --prototype-original-long-gop changes only the maximum GOP: 10 seconds instead of 2 when Original is selected. Compact stays unchanged. Both capture and splitting use the experiment. Without the flag, the existing modes remain unchanged.

Settings use a separate LocalAppData/Gaze-Prototype-LongGop/settings.json. Fresh prototype settings default to Original and 5 FPS. Check these before comparing. Normal Gaze settings are untouched. Recorded MP4s still use the configured output folder; choose a separate test folder if desired.

Compare a repeatable typing/scrolling/dialog workflow against normal Original and Compact, all at 5 FPS. Record file size, CPU, stop-to-saved time, text readability and timeline/Claude analysis. Larger GOPs can slow seeking. No recommendation to change production defaults yet.

Autostart should remain off: a Windows startup entry does not retain this experiment flag. Do not run normal Gaze concurrently because the existing single-instance guard applies.

## Initial measured result

Exact prototype parameters at 5 FPS produced 2.480 MB/min on the mostly-static synthetic scene, 4.033 on the changing scene and 8.003 on the dual-monitor scene, versus current Original 7.605 / 8.983 / 17.884. These normalize 12-second clips; savings were about 67% / 55% / 55%, not a guaranteed rate. Selected text-crop RGB error was 2.00–2.03 versus Original 1.92–1.94, so equivalent CRF does not establish identical pixel quality with a longer GOP. CPU and real-world readability still need validation.

The real capture fixture saved a playable MP4 and copied it to clipboard; it followed an owned dialog and paused/resumed. Stop-to-idle was approximately 0.17 seconds for this short fixture; this is not a sustained-workload comparison. Existing 33 regression checks passed. Local evidence remains in docs/factory/runs/prototype-long-gop and the synthetic matrix in the original Gaze checkout's docs/factory/runs/compression-benchmark.
