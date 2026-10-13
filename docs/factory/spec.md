# Gaze start at Windows sign-in

User request (2026-10-13): implement start at sign-in using Software Factory. Existing docs/requirements.md already specifies configurable silent tray startup.

## Scope and acceptance
- Add a clearly labelled Start at Windows sign-in switch to Settings, off for existing/fresh installations unless this Gaze startup entry is already registered.
- Register only the current user, no elevation, using a quoted absolute app executable path. Disable removes only Gaze's own registration. Read actual registration state when opening Settings; report registration failures and do not claim success or persist a false enabled state.
- Preserve other settings, normal silent tray startup and single-instance behavior. Never start recording automatically.
- Account for portable paths: document that moving/removing the executable invalidates startup and re-enabling repairs its path. Avoid registering dotnet.exe for development launches.
- Meaningful red-green tests cover enabling/disabling, paths with spaces, existing state, unrelated entries and failure handling via an injectable registration boundary. Do not modify this PC's real startup configuration during tests.
- Update README and CHANGELOG with behavior and limitations. Build app and run existing tests.

## Plan
One bounded Factory task: implement startup registration, Settings integration, regression tests and documentation. The user's implement instruction authorizes this local implementation and checks. No merge, deployment, publishing or enabling startup on this PC is included.
