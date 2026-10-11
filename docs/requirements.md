# Agreed behavior

Decision record: 2026-10-10. These requirements reflect the user discussion, not verified implementation capabilities.

## Operation and controls

- Start quietly in the Windows system tray at sign-in; configurable.
- Right-click opens quick actions; double-click opens Settings.
- Remember capture mode: selected window (default) or all monitors.
- Configurable global shortcut, initially Ctrl+Alt+Print Screen: idle -> armed -> recording -> stop/save -> idle. Ignore key repeats and report unavailable shortcuts.
- Arming locks the foreground window or confirms all monitors. Cancel arming with Esc or the tray menu.
- While recording, first Esc passes through and shows an excluded confirmation. A separate second Esc within one second stops and saves; after expiry continue. Ignore held-key repeats. Main shortcut stops immediately. Double-Esc can be disabled in Settings.

## Selected window

- Continue capturing the locked window when focus changes or unrelated applications cover it.
- Follow dialogs, nested dialogs, and identifiable windows spawned from the selected window. Do not indiscriminately follow every window from the same executable.
- Temporarily capture a followed window, then return to its surviving parent/original when it closes. Closing the original stops and saves.
- Browser navigation and displayed tabs within the captured window are included. Tracking separate spawned browser windows requires validation; no universal tracking guarantee yet.
- Minimize the original to pause; restore to resume. Recorded duration excludes paused time and pauses are omitted from output.
- Small movable duration overlay, with paused state, excluded from capture.
- Fixed video canvas during resizing and target switching. Configurable: original window size (default), or the original window's monitor resolution, measured when recording starts. Smaller windows are centered with black padding; larger windows scale to fit with a scaled indicator. The canvas remains fixed even when following a window on another monitor. This option applies only to selected-window recording.

## All monitors

- One video in the Windows monitor arrangement, at native resolution; gaps may be blank.
- Freeze layout/dimensions when recording starts. Disconnected monitor areas become blank; newly connected monitors join the next recording.
- No persistent overlay. Tray indicates recording and displays recorded duration on hover; no file-size indicator.
- Check encoder dimensions when arming. If unsupported, explain and require explicit selection of reduced resolution or window mode.

## Video and output

- No audio. Initial frame rate 5 FPS, configurable. Efficient compression of unchanged content.
- Visible cursor and configurable click highlight.
- One timestamped MP4 per session; no automatic splitting.
- Default folder: the user's Windows Videos folder/Gaze; configurable. Example Gaze_2026-10-10_14-35-22.mp4.
- After finalization, automatically copy the saved file using Windows file clipboard semantics, configurable. Keep Copy path available. Recipient compatibility belongs outside Gaze.
- Show Saving, then Saved and copied. On failure show the actual error and relevant retry. Distinguish saved-file success from clipboard failure; keep the saved recording and offer Copy again/Open folder.
- Quitting during recording stops, saves, copies, then exits.
- Record progressively to a working file; finalize to MP4 after stopping. Delete working files only after successful finalization. Interrupted-recording recovery is not supported; no startup recovery prompts.
- Monitor available disk space and stop/finalize with remaining headroom, explaining the reason. Threshold requires implementation validation.
- Exclude Gaze's overlays/confirmation from recordings, including all-monitor capture. Show completion notifications after capture ends.

## Settings

Shortcut, mode, window canvas size, frame rate, output folder, click highlight, automatic clipboard copying, launch at sign-in, and double-Esc behavior.

## Open technical decisions

Encoding backend/codec, supported Windows versions, reliable spawned-window attribution, behavior of simultaneous followed windows and multiple nesting levels, overlay exclusion support, disk headroom, and interrupted-session behavior require test evidence before production implementation.
