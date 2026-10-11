# Changed-frame recording investigation

Date: 2026-10-11. Scope: research and controlled encoding experiments; no recording behavior changed.

## Recommendation

Prototype **exact duplicate removal with preserved presentation timestamps**, alongside Compact compression. Compare the final composed image, including captured cursor and Gaze click highlight. Keep the recording's original active duration, including a static ending. Do not promise a percentage or a 30 MB ceiling before measuring representative recordings.

Do not start with FFmpeg's default `mpdecimate`: its similarity thresholds can discard a small textual or cursor change even when a frame is not identical. Exact comparison prioritizes evidence needed to describe screen actions. At 4K, frequent scrolling will still produce changed frames; an animated caret or cursor also limits the number of duplicates.

## Current Gaze pipeline

Repository evidence: `src/Gaze.App/Capture.cs` and `GraphicsSource.cs`.

- Windows capture copies the most recent surface into CPU bytes, rate-limited to configured fps. `CapturedFrame` currently has pixels and dimensions, but no timestamp or dirty-region metadata.
- A separate periodic timer renders the full composite and writes a complete BGRA frame into FFmpeg's rawvideo stdin. Unchanged images are written repeatedly.
- FFmpeg receives `-framerate fps`, H.264, Original CRF 18/veryfast or Compact CRF 24/medium, and `-g fps*2`. MP4 is fragmented at keyframes.
- Pause skips timer writes; output duration and recording filename are based on written-frame count divided by fps. Thus paused time is excluded, while slow writes can shorten recorded active time.
- Window capture adds Gaze's click highlight after composition. Monitor capture currently relies on captured cursor and does not add that highlight. Detection must follow existing rendering behavior.

## Timing is the main implementation requirement

Rawvideo has no header with timing information. Its input frame rate must be supplied externally. FFmpeg's rawvideo implementation assigns PTS/DTS from packet byte position divided by frame size. **Simply skipping unchanged writes into the current pipe produces a shorter video**; waiting longer between writes does not encode a matching hold. [Rawvideo documentation](https://ffmpeg.org/ffmpeg-formats.html#rawvideo), [official demuxer source](https://ffmpeg.org/doxygen/trunk/rawvideodec_8c_source.html).

For a first low-risk experiment, retain the periodic raw input and discard duplicates inside FFmpeg, preserving source timestamps. `-fps_mode vfr` passes timestamps while avoiding equal-timestamp frames; CFR can duplicate frames again. Do not use `setpts=N/FRAME_RATE/TB`, which closes the intended time gaps. This filter-only approach still pays the CPU-copy, composition, pipe-transfer and comparison costs. It measures encoding/storage benefit, not capture-side savings. [FFmpeg frame-rate modes](https://ffmpeg.org/ffmpeg.html#Advanced-options).

A production capture-side implementation needs timestamp-aware input (or direct encoder/muxer APIs), recording timestamps plus frame durations, and an active clock that subtracts pause intervals. Windows provides compositor QPC time through `SystemRelativeTime`; it must be normalized into a common recording clock and combined carefully for multiple monitors. [Microsoft frame timestamp API](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.direct3d11captureframe.systemrelativetime).

## Exact duplicates versus similarity

`mpdecimate` evaluates differences in 8×8 blocks, with high/low thresholds and a fraction of blocks allowed to change. Consequently its defaults mean “sufficiently similar,” not “identical.” It can suppress a small edit on an otherwise unchanged screen. An experiment can compare defaults against zero thresholds, but zero-threshold filtering still compares the planes/pixel format presented to the filter; it is not necessarily a BGRA byte comparison before color conversion. For a guarantee that every captured pixel change remains a candidate frame, compare final BGRA pixels, using a fast hash only as a precheck followed by equality verification. [FFmpeg mpdecimate](https://ffmpeg.org/ffmpeg-filters.html#mpdecimate).

## Final hold, keyframes, and crash recovery

The final still image requires an explicit final duration/end marker. A duplicate-removal filter can discard every trailing frame and leave the last retained frame with an insufficient end duration. This is an implementation hazard to demonstrate with a static-ending test, rather than assume the muxer repairs it.

The current GOP is specified as a count of frames. With sparse VFR, a count of ten no longer means approximately two seconds. Revisit keyframe scheduling in elapsed active time, and inspect seek behavior and crash recovery. MP4's `frag_keyframe` starts a new fragment at each keyframe, so fewer keyframes can change how often recoverable fragments are written. [FFmpeg libx264 GOP options](https://ffmpeg.org/ffmpeg-codecs.html#libx264_002c-libx264rgb), [MP4 fragmentation documentation](https://ffmpeg.org/ffmpeg-formats.html#Fragmentation).

Do not remove every recovery boundary solely to maximize compression. Longer keyframe intervals are a separate experiment: they may be responsible for part of the storage benefit attributed to duplicate removal.

## Windows dirty regions

Microsoft documents `Direct3D11CaptureFrame.DirtyRegions` and `GraphicsCaptureSession.DirtyRegionMode`, but the pages retrieved for this investigation are prerelease-marked and do not expose a reliable minimum-build table. Gaze targets `windows10.0.19041.0`; a feature available on the development laptop must not be assumed available on another Windows installation. Use compatible SDK bindings, runtime `ApiInformation.IsPropertyPresent` checks, and a complete-frame fallback. [Frame dirty regions](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.direct3d11captureframe.dirtyregions), [session dirty-region mode](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.graphicscapturesession.dirtyregionmode), [runtime property detection](https://learn.microsoft.com/en-us/uwp/api/windows.foundation.metadata.apiinformation.ispropertypresent).

Dirty-region metadata is a potential optimization for avoiding full readbacks, not a replacement for timing correctness or final composite comparison. Gaze overlays and layout/window-size changes can alter the output even when a captured surface has no new dirty region. A version-independent full-image comparison is the practical baseline.

## Validation gates for a future selectable mode

1. Static recording and a long static ending retain active duration within one configured sample interval.
2. Single-character edits, tiny notifications, cursor movement and click highlights survive.
3. Scrolling and rapid switching preserve all sampled changed frames.
4. Pause time is excluded; resume does not create a large timestamp gap.
5. Window resize, target switch and multiple-monitor composition remain correct.
6. FFmpeg stop, partial-file recovery and seeking are tested with sparse frames.
7. Compare Original, Compact, and Compact with changed frames using identical source pixels. Report bytes, frame count, end timestamp, visual fidelity and CPU time separately.

The user's screenshot (approximately three minutes, 4K, 44.5 MB) is context, not an accessible source recording. Synthetic results cannot establish its eventual size or Claude's interpretation accuracy.

## Controlled 4K experiment — 2026-10-11

Evidence: `docs/factory/runs/changed-frames-001/experiment.py`, `results.json`, generated PNG sources, MP4 outputs and decode logs (local, Git-ignored). All inputs were synthetic, no desktop/user content. Full development FFmpeg was used; the minimal shipping encoder's filter availability was not validated. No production code was changed for this investigation.

Twenty seconds at 3840×2160, 5 fps. Quiet case: four screen states, one change every five seconds. Cursor case: twenty states, one cursor move per second. Same CRF/preset within each profile, same YUV420 conversion and fragmented MP4 flags as Gaze. VFR selects exact-known synthetic states, not a similarity threshold, with an explicit terminal sample at 19.8 s. Both synthetic cases have mostly unchanged text, no scrolling: these numbers are favorable cases, not forecasts for user recordings.

| Compact / CRF 24 medium | Quiet screen | Cursor once per second |
|---|---:|---:|
| Current: 5 fps, keyframe limit 10 frames (~2 s) | 2,914,171 bytes | 2,904,783 bytes |
| Changed states + final sample, keyframe limit 10 retained frames | 308,782 bytes (89.4% smaller) | 1,004,682 bytes (65.4% smaller) |
| All 5 fps, keyframe limit 50 frames (~10 s) | 672,834 bytes (76.9% smaller) | 677,175 bytes (76.7% smaller) |
| All 5 fps, keyframe limit 250 frames (~50 s) | 329,329 bytes (88.7% smaller) | 337,038 bytes (88.4% smaller) |

The 250-frame setting is an explanatory upper experiment, not a shipping recommendation. Sparse VFR with a frame-count GOP can be worse than CFR with a long GOP: in the cursor case VFR reaches ten retained frames repeatedly and creates expensive new keyframes. Therefore the size gain cannot be attributed only to duplicate removal. Current Gaze's short GOP is a substantial opportunity for largely static 4K screens. Reported encoding wall time (including repeated PNG decoding, not CPU usage): quiet Compact CFR ~6.8 s vs VFR ~0.65 s; cursor Compact CFR ~7.4 s vs VFR ~1.9 s. Gaze's live capture/readback cost was not measured.

All 12 main outputs decoded successfully. CFR kept 100 frames, VFR kept 5 (quiet) or 21 (cursor), with source transitions at their intended timestamps and final PTS 19.8 s. CFR container duration was 20.00 s; VFR duration reported 20.20 s after explicitly setting PNG nominal rate to 5. An earlier run with the image demuxer's default 25 fps reported 20.04 s. This exposes an unresolved final-duration/fragmented-muxing issue; the prototype is not ready to ship. The initial rejected `-r 5` plus `-fps_mode vfr` combination was replaced with `-enc_time_base 1:5`. No source timestamps were renumbered.

A separate mpdecimate test on raw synthetic PNG input retained states at 0, 5, 10 and 15 s for both defaults and zero thresholds; it discarded the trailing still frames through 20 s. These particular text changes survived default thresholds, but this does not prove single-pixel or every small text change is safe. Final duration needs explicit treatment even when source PTS survive.

## Recommendation after measurements

First trial a longer time-based keyframe interval (for example 10 seconds) in Compact, using actual 4K recordings. It is a much smaller change than replacing the rawvideo input timing architecture and preserves every currently sampled frame. Check seeking/finalization and the longer interval between written fragments; do not promise an upload cap. Separately design changed-frame mode if CPU or additional storage savings justify it: exact final-canvas comparison, timestamp-aware stream input, active-time clock, explicit final sample duration, time-based keyframes and compatibility tests. Avoid default similarity-based filtering for transcription/OCR.

Next required evidence: the user's 44.5 MB recording, small text/one-character edits, rapid cursor/click events, scrolling, pause/resume, live 4K throughput and Claude interpretation. No settings option for changed frames has been added, and no app behavior has been changed by this research.
