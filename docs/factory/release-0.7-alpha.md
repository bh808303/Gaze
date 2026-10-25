# Gaze 0.7 Alpha release validation

2026-10-25. Includes the reviewed Screenshot trigger, rapid-close fix, persistent Output folders and category file panel. Feature Factory reviews passed before merge. Installer version is 0.7.0; UpgradeCode is unchanged.

Merged-main Release build/core checks, output persistence/containment tests and actual settings fixture tests passed. Built a fresh self-contained MSI using the existing matching minimal FFmpeg/x264 encoder. Packaged corresponding encoder source and SHA256SUMS.txt together. Administrative MSI extraction exited 0 without installing. Windows Installer metadata reports ProductVersion 0.7.0. Extracted App/Core/Panel DLLs and FFmpeg/FFprobe hashes match the publish payload. Extracted output-panel and injected trigger probes both exited 0. Evidence: artifacts/release-0.7-evidence (ignored local run artifacts).

The initial direct GUI invocation did not reliably wait for an exit code; reran both probes using Start-Process -Wait and verified exit 0. No real clipboard, input hooks or recycle operations were exercised by those contained probes. Full clean-device installation/upgrade/repair/uninstall and broader physical input, clipboard/Claude, GPU/DPI/multi-monitor testing remain outside this alpha validation. Installer is unsigned. Existing untracked files in the main checkout were preserved.
