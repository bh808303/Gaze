# Gaze 0.4 alpha

Gaze is a Windows tray application for recording computer work so AI agents can turn visible actions, clicks, and screen changes into a written timeline, activity log, or work description. This visual “transcription” uses silent screen recordings rather than spoken audio. Capture a selected window, the main monitor, or all monitors, then paste the saved file into an AI application for analysis.

The recording format is designed to keep files as small as practical while preserving the screen text and actions an AI agent needs to understand the work. Gaze saves H.264 video in MP4, using a low default frame rate and selectable compression. Compact mode adds stronger compression and less frequent full keyframes to reduce repeated storage of unchanged screen content. The aim is efficient sharing and AI analysis; the useful compression level depends on text size and screen activity, so smaller files must still be checked for readability and missed brief actions.

## Controls

- Left-click the tray icon: arm → start recording → stop and save.
- Right-click: open the recording panel and Settings.
- The panel always aligns with the bottom-right corner of the working area on the monitor containing the pointer when opened, keeping clear of the taskbar. Its size is limited to that working area. Drag the logo header to move either the main panel or Settings; header buttons remain clickable. After dragging, switching between pages keeps the manually chosen position. Reopening from the tray restores the bottom-right placement.
- Default shortcut: Ctrl + Alt + PrintScreen; configurable in Settings.
- Press Esc twice within one second to stop. The first press remains available to the application being recorded.

The tray icon is green when ready, orange when armed or saving, and red when recording.

## Recording

Video only, with no audio. The default is 5 frames per second, preserving native text resolution. Selected-window capture follows owned dialogs, returns to the original when they close, and stops when the original closes. Window canvas options are Windows size and Monitor size. Main monitor captures the Windows primary display; All monitors captures the desktop across displays. Highlight mouse clicks applies to all three capture modes, drawing a gold ring for 400 ms when a left-button press is detected at a captured frame; very short clicks between frames can be missed.

Settings → Compression offers Original (H.264, CRF 18, veryfast, maximum 2-second keyframe interval; existing default) and Compact (H.264, CRF 24, medium, maximum 10-second keyframe interval). Compact preserves resolution and frame rate, uses more CPU, and may soften small text. The choice is saved and applies to the next recording. File size depends on content and duration; neither mode guarantees a file under 30 MB. Older settings retain Original. Compact avoids repeatedly encoding full static screens; scene changes may still create earlier keyframes. Seeking can require decoding up to 10 seconds from the preceding keyframe. MP4 fragments are also written less frequently; interrupted-recording recovery remains unsupported.

Saved names use `Gaze_d101026_t2105-2107_2min.mp4`: local date and start/end times, with whole recorded minutes rounded down. Pauses are excluded from duration. A numbered suffix prevents overwriting files.

Settings → Saving → Split recordings over 29 MB is off by default. When enabled, recordings larger than 29,000,000 bytes are re-encoded after stopping into ordered `-part1.mp4`, `-part2.mp4` files. Each finished part is checked against the limit and subdivided if needed; resolution, frame rate, and frame order are preserved. Parts use the selected compression, which can add compression loss. Saving takes longer and temporarily needs space for the original and parts. The original is removed only after every part is saved successfully; on failure it is retained and an error is shown. Files already at or below the limit stay untouched. A frame too large to fit by itself causes a safe failure. Auto-copy places all resulting parts on the clipboard; individual parts remain available in the recording panel.

Automatic clipboard copying places a file reference on the clipboard. Open, Open Folder, Copy Clipboard, and Clean-up are available in the panel. Clean-up asks for confirmation and moves MP4 files directly in the recording folder to the Recycle Bin; subfolders and unfinished working files are retained.

Settings → **Start at Windows sign-in** registers Gaze for the current user only, without elevation, using the quoted absolute executable path in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (value `Gaze`). It defaults off unless that entry already exists. Settings reads the actual Windows registration each time it opens; Save applies the switch and reports registration failures. Cancel leaves startup unchanged. Disabling removes only Gaze's value. Startup opens silently in the tray and never starts recording automatically. Windows policy or Startup Apps controls can prevent a registered app from launching.

For portable builds, moving or removing the executable invalidates the registered path. Open the app at its new location and save Settings with the switch enabled again to repair it. Development launches through `dotnet.exe` cannot enable startup; launch `Gaze.App.exe` instead. Registration checks use an injected in-memory boundary and do not change Windows startup configuration; an actual Windows sign-in has not been exercised by these tests.

Interrupted-recording recovery is not supported. Normal startup is silent. The idle panel can be captured by screenshot tools; it is excluded while armed, recording, or saving on supported Windows versions.

The tray panel's split toggle appears directly below Save folder. Its value is loaded and saved through the same persistent settings as recording and is retained when changing other panel settings.

## Embedded activity timeline

Settings → **Record timeline metadata** enables one optional factual bundle for the next recording. It defaults off for both new and older settings. The basic window/title, button-press and pause/resume timeline stays active; **Keyboard events in timeline** remains a separate opt-in. The bundle adds button releases, vertical/horizontal wheels with original signed raw deltas, changed-position pointer samples (at most 10 Hz normally, approximately 30 Hz while a delivered button press is held), neutral pointer boundary transitions, geometry/mapping changes, and actual encoded-frame sampling times. It does not interpret gestures, intent, dialogs, cursor shapes or visual changes, and does not collect Windows logs.

New schema-v2 metadata declares collection options and known hook/queue status. Pointer sampling and pending queues are bounded; overflow is counted rather than hidden. Geometry is recorded only when its mapping changes. Movement sampling can miss brief crossings, and boundary/hit-testing reflects the desktop when events are consumed. Actual sample times expose scheduling gaps, but do not promise each encoded image is a newly delivered Windows capture frame. Silent Windows hook removal, secure desktop activity and input never delivered to a hook cannot be detected. Dense event frames are split into safe text packets; a single oversized event or exhausted packet time slots retain plain video with a warning. See [exact fields and limitations](docs/event-timeline.md).


Settings includes **Keyboard events in timeline**, off by default. Enable it before recording to store every delivered key press/release, including modifiers and repeats, within the capture boundary. Window mode accepts foreground input from the recorded window or its owned dialogs; monitor modes require foreground-window overlap with captured monitors. Paused and unrelated input is excluded. Events contain virtual-key labels, scan codes, down/up state and timestamps rather than translated text. These key codes can reveal sensitive typing, including passwords within the recorded app. The hook only queues input; filtering and timeline updates run on the consumer. Keyboard events survive splitting and are extracted by the supplied reader. Secure desktop input and custom popup behavior remain outside the tested coverage.

Every new recording automatically includes process/window-title chapters (captured windows in window mode; foreground windows in monitor modes), an event text track, and a `gaze_timeline` clock-mapping tag. Window mode retains titles only for the recorded window and its owned dialogs; unrelated foreground activity becomes a neutral `external: true` event without process, title or window ID. Monitor modes retain titles only for windows overlapping a captured monitor. Overlap includes the entire title even if only part of the window is visible. Clicks are restricted to the captured window bounds or actual captured monitor rectangles, excluding desktop gaps. These rules apply automatically to new recordings; existing files retain their metadata. Mouse button presses are captured independently of the video frame rate and Highlight clicks setting. Click events inside the captured area include screen coordinates and coordinates in the encoded video. Paused input is excluded. Events carry their actual clock time with UTC offset and the most recently sampled video frame; frame positions are not claims that the action is visible in that exact image. Start/resume anchors identify the actual sampled frame and its clock time; resume anchors also retain the transition request time. Each split part has its own frame positions and exact first-frame clock time, with the active window carried over.

The final metadata rewrite copies the encoded video without re-encoding. The event track is disabled by default; players can still enable subtitles according to their own preferences. Chapters and text can be extracted without reading images. Splitting still re-encodes video, attaches only each part's timeline, and checks its size after metadata is added. Metadata failure leaves the playable plain video and writes `Gaze-warnings.log` in the recording folder. Event-hook failures also log a warning. Saving uses temporary files and extra disk space; no fixed completion time or text-token count is guaranteed.

Read all events, chapters, and clocks with one command (requires Python 3):

```powershell
python ./artifacts/app-event-timeline/Read-Timeline.py 'C:\path\recording.mp4'
```

The portable app includes FFprobe beside the reader. From the repository, supply `--probe .tools/encoder-build/output/ffprobe.exe`. See [timeline format](docs/event-timeline.md) for the versioned event schema. Existing recordings are not retroactively changed.

## Claude recording-analysis skill

The optional [gaze-video-analysis skill](skills/claude/gaze-video-analysis.zip) helps Claude analyze Gaze recordings efficiently. Import the ZIP as a custom skill in Claude, enable it, then upload a Gaze MP4 and describe the result you want. The skill recognizes Gaze filenames, including upload prefixes and numbered split parts. Without additional instructions, it produces a summary, a timeline with video and clock times, a work description suitable for a ticket or time entry, and a list of uncertainties.

It reads the embedded timeline first to find window changes, clicks, pauses and optional keyboard activity. It then selects changed or relevant video frames, builds contact sheets, and crops readable details when needed. This avoids reviewing thousands of nearly identical images. Split parts should be supplied in order. Older recordings without metadata can still be analyzed visually, but their clock times are approximate and cannot reliably account for pauses.

The skill requires a Claude environment that can run Python with NumPy, Pillow and a full FFmpeg build capable of decoding video and extracting subtitle metadata; its preparation script can use `imageio-ffmpeg`. Gaze's minimal bundled encoder is not a substitute for that analysis dependency. The original video remains unchanged. Large recordings can still take substantial processing time, and reported token counts are estimates rather than billed usage.

Analysis distinguishes visible evidence from inference: a click does not prove its effect, keyboard labels do not reconstruct exact typed text, and unreadable text or activity outside the capture boundary remains uncertain. The skill instructs Claude to redact credentials and other secrets. It has been tested repeatedly by the project owner in Claude. Analysis runs in Claude, separately from the Gaze recorder.

## Build and run

Requires Windows 10 version 2004 or later, x64, and the .NET 10 SDK specified by `global.json`.

```powershell
./scripts/Build.ps1 -Release
```

Place `ffmpeg.exe` beside `artifacts/app/Gaze.App.exe`, or set `GAZE_FFMPEG` to its executable path. Gaze requires an FFmpeg build with the libx264 encoder. Splitting also requires the MOV/MP4 demuxer, H.264 decoder, and trim/setpts filters; rebuild the bundled encoder using the updated build script when packaging this feature. The development build also copies a local encoder from `.tools` when available; this directory is excluded from Git.

Event metadata additionally requires FFmetadata and SRT demuxers, the SubRip decoder, and the mov_text encoder (`movtext` in FFmpeg configure component names). The rebuilt bundle includes FFprobe and the mov_text decoder for timeline inspection. `scripts/Pack-EncoderSource.ps1 -OutputDirectory <portable-app-directory>` packages the matching complete encoder source and writes SHA256 checksums beside a portable build; omission keeps the installer output directory.

Launch `artifacts/app/Gaze.App.exe`. The optional `Start-Gaze.ps1` launcher builds when the expected application or adjacent encoder is missing. Only one tray instance runs at a time.

## Alpha status and validation

The alpha installer release includes the application, .NET desktop runtime, and a private FFmpeg encoder. WPF UI 4.3.0 provides the interface controls. The panel uses software rendering and is positioned while hidden to avoid blank windows and placement flashes. It is recreated after saving, a recording failure, or cancelling an armed recording (Esc or Cancel) to discard the capture-excluded rendering surface. The next panel opening creates fresh controls.

Core and split checks cover recording names, target locking, paused duration, saving state, Esc confirmation, size boundaries, frame ordering, collision handling, and original retention after failure. Run the real video split check with `dotnet run --project tests/Gaze.Tests -- --encoder <full-ffmpeg-path>`; it encodes a recording over 29 MB, splits it, and decodes every part to verify the total frame count and size limit. Local synthetic recording checks cover decoded video, dialog following, overlay exclusion, and clipboard copying. Desktop rendering checks cover panel placement and recreation. Local evidence, recordings, build outputs, and dependencies are excluded from Git.

To verify the minimal bundled encoder, add `--split-encoder .tools/encoder-build/output/ffmpeg.exe` to the video check. A full FFmpeg build is still used to generate the test source and decode the results. Video checks cover 1 and 5 fps with Original compression, and 30 fps with Compact compression.

Run settings/default/startup-option checks with `dotnet run --project tests/Gaze.Settings.Tests`. Run reader compatibility/filter tests with `python -m unittest discover -s tests -p test_timeline_reader.py` (or `py` where Python is installed under the Windows launcher).

Append `--metadata-tests .tools/encoder-build/output/ffmpeg.exe .tools/encoder-build/output/ffprobe.exe` for compressed-video packet equality, chapters, title escaping, disabled subtitle disposition, graceful metadata failure, and size checks after metadata pushes a video over the limit. Set `GAZE_TEST_PYTHON` if Python is not on PATH. The synthetic `--self-test --event-check --no-click-highlight --no-overlay --evidence <directory>` check captures only its own test window and verifies five rapid clicks, title changes, chapters, pause/resume anchors, timeline extraction, and clipboard saving. It moves/restores the pointer to inject test clicks into the fixture and requires Windows capture permission.

Broader GPU/DPI/multiple-monitor validation, browser-spawn attribution, and actual Windows sign-in execution remain outside this alpha's guarantees. The Gaze recorder does not perform AI analysis itself; the optional Claude skill above provides that workflow separately.

See [CHANGELOG.md](CHANGELOG.md) and [requirements](docs/requirements.md).

## MSI installer

Download the unsigned x64 MSI from the [installer release](https://github.com/bh808303/Gaze/releases/tag/v0.4.0-alpha-installer). It includes FFmpeg and .NET, a Start-menu shortcut, and uninstall registration. The same release includes encoder corresponding source and SHA256 checksums. See [installer/README.md](installer/README.md) for build instructions, installation behavior and validation limits.
## Transcribe mode (in development)

Transcribe collects window/rectangle screenshots into an ordered ZIP instead of recording MP4. See [Transcribe usage and validation limits](docs/transcribe.md). Temporary ZIP clipboard compatibility with Claude Desktop is proven by the standalone probe; integrated native acceptance is still in progress.

## Compact tray menu
The default menu shows recording controls, status, and capture mode. The header folder icon opens the recordings panel; its back arrow returns to the compact menu. The settings cog opens settings. Recording actions and Quit remain available in the recordings panel. The compact menu grows while armed to keep Cancel arming accessible.

Visual checks: use `--panel-preview <PNG path> --check-navigation` for navigation checks, `--preview-recordings` for the recordings panel, or `--preview-settings` for settings. Preview uses an isolated sign-in startup value and does not access the Windows startup registry.

