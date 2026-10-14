# Transcribe window interface

Transcribe reuses the main panel's last displayed position, including a dragged position. When the panel has never been displayed, it opens at the bottom-right of the cursor monitor's working area. Placement is constrained to the working area and repeated after showing to account for monitor DPI.

Transcribe now uses the main/settings panel's zero minimum dimensions and content template, allowing its 332.35-wide frame to fill the window without extra side strips.

Validation:
- `dotnet build src/Gaze.App/Gaze.App.csproj --no-restore -m:1 -p:UseSharedCompilation=false -v minimal`: exit 0, zero warnings/errors.
- `dotnet run --project tests/Gaze.Tests/Gaze.Tests.csproj --no-restore -p:UseSharedCompilation=false`: exit 0, all 46 checks passed.
- `git diff --check`: exit 0.
- Live visual and mixed-DPI placement verification remains outstanding.

Visual preview: `Gaze.App.exe --transcribe-preview <absolute PNG path>` renders the actual empty Transcribe window without input capture or settings changes. `transcribe-window-preview.png` confirms the frame fills the window without side strips; desktop positioning remains unverified.
