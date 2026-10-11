# Gaze event timeline v1

New MP4 files have three complementary representations:

- Chapters named `process — title` at captured-window/title changes in selected-window mode, or foreground window/title changes in monitor modes.
- A disabled-by-default `mov_text` track with handler name `Gaze events`. Each timed cue is `gaze-base64-v1:` followed by base64-encoded UTF-8 JSON: `{"version":1,"events":[...]}`. This protects backslashes, newlines and markup from SubRip conversion. Events sharing a video frame share a cue. Empty MP4 subtitle-clear packets have no events.
- A global `gaze_timeline` tag containing version, frame rate, frame count, original recording start, the part's original first frame, exact part start time, and clock anchors. The tag's events array is empty; events live in the timed track.

`Read-Timeline.py` returns these as one JSON object, replacing the empty events array with all timed-track events and adding chapters in seconds. It uses one FFprobe subprocess, not image extraction. Run `python Read-Timeline.py recording.mp4`; pass `--probe <ffprobe-path>` if FFprobe is not beside the script or on PATH. Missing metadata produces an error and exit code 1.

The reader also accepts legacy plain-JSON cues. Malformed event packets produce warnings on stderr and are skipped; valid packets remain available in the JSON output. Corrupted legacy titles cannot be recovered automatically.

## Positions and clocks

The low-level mouse hook only timestamps and queues clicks; it never waits for drawing or writes files. The encoder drains accepted clicks before advancing to the next sampled frame. Pause and stop drain accepted clicks before closing that interval, while clicks received during a pause are ignored. Warning callbacks enqueue messages; the encoder writes them outside the drawing lock. A contention test holds the consumer blocked while 1,000 further clicks are queued, then checks order and original timestamps. This verifies queue isolation rather than promising Windows will never remove a hook for other reasons.

Clicks drained while a resume is pending are buffered in the timeline until its first resumed sample. Their frame indices then point to that sample, with original click clock times unchanged. If recording stops or pauses again before any resumed sample exists, that unsampled interval is discarded.

`frame` is zero-based in the current file. `recordingFrame` is the original recording's frame index. Video seconds = frame / framesPerSecond. Video duration excludes pauses. An event is associated with the most recently sampled frame at the time its hook is processed. Multiple short clicks between two frames remain distinct events; this does not imply those clicks are visible in a low-frame-rate video.

`clockTime` values are ISO 8601 wall-clock timestamps including UTC offset. Mouse/window hook times compensate for message-dispatch latency using the Windows millisecond clock. They are system-clock measurements, not independently verified time. Clock changes retain the offset and reported time.

`clockAnchors` have frame, recordingFrame, clockTime, reason (`start`, `resume`, `part-start`) and, for recording transitions, `transitionTime`. For resume, clockTime is the actual first resumed sample; transitionTime is the request time. Each part has a frame-zero anchor and partStartTime from its actual first captured sample. Arbitrary intermediate frame clock times should not be assumed exact from nominal FPS if encoding ran slower than real time; event timestamps and the recorded anchors are explicit measurements.

## Events

All events have frame, recordingFrame, clockTime, kind, and carryOver. Optional fields are omitted when absent.

Output events are stably sorted by local frame, then clockTime, after split context is inserted. Equal frame/time pairs retain their original processing order. Thus subtitle cues and reader output follow event timestamps within each frame rather than callback/queue processing order. Frame order takes precedence if the system wall clock moves backwards.

| Kind | Additional fields | Meaning |
| --- | --- | --- |
| start | None | First sampled frame |
| window | process, title, windowId, scope, external | Window/title change with scope `foreground` or `captured`; title retained within capture boundary; external events omit process, title and windowId |
| click | button, screenX, screenY, x, y | Button press inside captured area; desktop screen pixels plus encoded-video pixels |
| pause | None | Capture pause request |
| resume | None | Resume request, associated with first resumed frame |
| part-start | None | Timing marker for a part with no other events |

Buttons: left, right, middle, x1, x2. Encoded x/y account for window centering/scaling or monitor desktop origin; screen coordinates may be negative. Window IDs are hexadecimal HWNDs, useful only within the recording, not stable application IDs. A carried-over window event starts at local frame zero but retains its original recordingFrame and clockTime; it is context, not another switch.

Each part gets events within its own frame range, rebased to zero, and the preceding context for each scope when needed. Captured-window context follows owned dialogs even if a different app has foreground focus; foreground context remains separately available in the text track. Chapters use the current file's frame time base. Events occurring while paused are not added. A resume emits a window event only if that scope's window/title changed. Chapters may merge several title changes that occur within one sampled frame; the text track retains those individual events.

## Failure and size behavior

Metadata is added to a new temporary MP4 using video stream-copy. FFmpeg 7.1 enables its first subtitle track regardless of requested disposition; Gaze clears only the Gaze-events track's MP4 `tkhd` enabled bit after muxing. Packet hashes are tested unchanged. Other player subtitle preferences may still enable the track.

The original plain video is replaced only after the remux and track flag update succeed. Failure retains it and appends a warning to Gaze-warnings.log. An event cue exceeding the timed-text packet budget causes the same safe fallback instead of silently truncating JSON. Temporary metadata/SRT files are removed. Interrupted-process recovery remains unsupported.

When splitting is enabled, each completed part is checked against 29,000,000 bytes after adding its metadata. A small original that grows over the cap is split too. Splitting re-encodes from the original and then stream-copies metadata onto the part. The original is removed only after all size-checked parts are published.

Source references: [FFmpeg stream-copy and metadata mapping](https://ffmpeg.org/ffmpeg.html), [FFmetadata and MOV/MP4 metadata](https://ffmpeg.org/ffmpeg-formats.html), and the bundled FFmpeg 7.1 source `libavformat/movenc.c:enable_tracks` / `libavformat/mov.c:mov_read_tkhd` for subtitle defaults.

## Capture boundary

Window mode checks root-window and owned-dialog relationships before reading a title or process name. A foreign foreground window produces a neutral `external: true` event, clearing prior foreground context without storing its identity; foreign title changes are deduplicated as the same neutral context. Captured-scope title notifications follow the currently captured root, including child notifications normalized with GA_ROOT. An owned dialog has its own root and is allowed through the recording's owner relationship, not just a shared process ID. Monitor modes require overlap with an actual captured monitor rectangle, including Main monitor mode; foreground location changes refresh that boundary check. Whole titles are retained for partly overlapping windows. Clicks outside the captured window bounds or outside all captured monitor rectangles are discarded. Region capture is not currently available; no new title settings or exclusion list are added. Existing recordings are unchanged.

External context survives pause/resume and split carry-over without identity fields. A monitor-mode external chapter uses the neutral label `Outside recorded area`; window-mode chapters continue to describe captured windows rather than unrelated focus changes.
