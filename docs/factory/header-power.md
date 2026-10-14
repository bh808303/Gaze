# Header power button

The main/settings/recordings header has a Quit Gaze power icon between Settings and Close. It calls the existing Quit handler, preserving save-before-exit behavior and Transcribe close confirmation.

Validation: Release build to artifacts/header-power succeeded with zero warnings/errors. Actual panel preview exited 0 and was visually inspected. `git diff --check` passed.
