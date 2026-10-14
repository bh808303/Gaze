# Compact menu verification

2026-10-14: Compact home menu with folder navigation to recordings, settings navigation unchanged. Quit remains in recordings view. Armed mode expands to show Cancel arming.

- Release build: `dotnet build src/Gaze.App/Gaze.App.csproj --no-restore -m:1 -p:UseSharedCompilation=false -c Release -o artifacts/compact-menu -v minimal`: exit 0, zero warnings/errors.
- Actual WPF navigation check via `--panel-preview <path> --check-navigation`: exit 0. Checks home, recordings, settings, back navigation, armed cancel visibility, and return to idle. Allows one DIP for native DPI rounding.
- Settings preview: exit 0. Main and recordings previews inspected visually.
- `git diff --check`: exit 0.
- Normal release destination was locked by the running app; updated executable is artifacts/compact-menu/Gaze.App.exe.
- Preview startup reads are isolated from the registry. Production startup registration behavior is unchanged.
