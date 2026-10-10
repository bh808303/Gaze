# Prototype plan

Build a small Windows capture proof of concept before the complete tray/settings application. Select the implementation stack after inspecting available Windows tooling and current official capture/encoding documentation.

1. Capture a locked window at native resolution/5 FPS. Verify readable static text, scrolling, coverage by another app, focus changes, minimize/restore.
2. Exercise owned/nested dialogs and separately spawned browser windows. Record what can be attributed reliably; never treat shared process identity alone as proof of origin.
3. Test fixed-canvas policies with differently sized windows/dialogs and resizing. Compare padding and scaling for text readability; bring any behavior change back to the user.
4. Verify cursor/click capture and excluded timer/Esc confirmation for window and all-monitor modes. Check actual video frames, not only the live UI.
5. Test progressive recording, forced interruption, recovery, final MP4, disk-space stop, and file clipboard copying. Verify recovered files play and record the lost tail.
6. Test multiple monitor dimensions, arrangement, unplug/reconnect, and encoder dimension checks. Measure representative size/resource usage without promising a fixed two-hour file size.

## Evidence

Keep commands, environment details, results, and limitations in docs/factory/runs/. Local recordings belong in recordings/ and must not be committed. Evidence summaries may be committed separately after checking for sensitive captured content. No test has run yet.

## Exit gate

Agree on remaining technical policies using observed results, then build tray controls, settings, startup, and the recording state machine. Do not enable sign-in startup during prototype experiments.
