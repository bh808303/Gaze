# Gaze

A Windows tray application for recording a selected window and its spawned windows, or all monitors. Video only; no transcription or AI analysis is part of the application.

Status: repository initialized; implementation has not started. See [requirements](docs/requirements.md) and [prototype plan](docs/prototype-plan.md).

This is an independent Git repository nested inside Software Factory. Application code and evidence belong here. The factory's launchers remain in the parent directory.

## Development gates

Validate capture, target switching, readable text, overlay exclusion, and interruption recovery before committing to the production architecture. Record actual results in this repository. Browser spawn tracking, fixed-canvas sizing, codecs, hardware limits, and recovery guarantees remain unverified. A working prototype must not be described as a finished application.
