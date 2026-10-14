# T02: in-memory OLE virtual ZIP probe

Current continuation: see **Human-confirmed Claude result** below and **Revised T02**. Historical no-temp instructions describe the original virtual probe and are superseded for the new file-drop probe by the appended human approval.

## Human-confirmed Claude result — 2026-10-14

The parent launched artifacts/transcribe-lifecycle-debug/Gaze.App.exe with --transcribe-file-drop-probe outside restricted execution. A subsequent desktop-context process query confirmed a responsive window titled `Gaze T02 temporary ZIP file-drop probe`. Restricted-context launches did not persist/access the same desktop process context; a process ID alone was insufficient confirmation of a usable probe.

The human confirmed: "ok probe run and zip was pasted in claude ill reurn its output". They then supplied Claude's result: the ZIP contained one frame plus instructions.txt, with ordered transcription, overlap merging, deduplication, heading preservation and unreadable-text instructions. Claude identified 000001_probe.png and transcribed `T02-IN-MEMORY-7319`, matching the generated fixture exactly. Claude noted the zero/O distinction and correctly read the zero.

This is human-reported evidence of three separate successes for the temporary final-ZIP/file-drop route in the tested Claude installation: attachment paste, ZIP/instructions access, and PNG text reading. It does not validate virtual in-memory ZIP compatibility or establish universal application support. The original virtual route failed to attach. Exact Claude version, observed file-write tracing, minimize/repeat-paste lifetime and graceful Stop cleanup verification remain unrecorded. The probe must stay open until the consumer finishes reading. Automated staging/ownership/cleanup checks passed previously and were rerun successfully in this continuation; they do not replace these remaining native observations. T02 is not yet marked complete.

### Subsequent lifetime/cleanup observation

Desktop-context process inspection identified Claude ProductVersion/FileVersion 2.37337.1; Windows NT 10.0.26300.0. After Claude ingestion and human reply, the probe was still responsive. At that point the clipboard no longer advertised the probe ZIP (clipboard replacement itself was not attributed to any particular application). Graceful --stop-transcribe-file-drop-probe returned 0; the probe process exited and the probe staging root contained zero remaining gaze-transcribe-probe.zip files. No recursive deletion command was issued: the probe performed its exact-file ownership-aware cleanup. Minimize/repeat paste and runtime write tracing remain unperformed. This closes the observed graceful Stop scenario, not all receiver/lifetime permutations.

Recorded 2026-10-14. Scope: approved T02 only; AC11/AC13 feasibility. Probe and automated producer/consumer checks are implemented. **T02 remains incomplete pending manual Claude Desktop validation.** `tasks.json` is unchanged. No production export integration, later tasks, commits or Claude messages.

Native app UI control is unavailable in this agent session. This did not prevent the authorized isolated COM checks. The real clipboard probe was **not launched**. Automated checks observed an unchanged real clipboard sequence number. No original clipboard contents were read, saved, replaced or restored.

Environment observed: `Microsoft Windows NT 10.0.26300.0`; .NET SDK `10.0.303`; x64 builds. Claude Desktop version: **not verified**. No claim of receiver compatibility.

## Reviewable implementation

- `src/Gaze.App/TranscribeClipboard.cs`: immutable byte snapshot, one Unicode `FileGroupDescriptorW` in movable HGLOBAL, indexed `FileContents` at lindex 0 in a fresh native HGLOBAL-backed IStream. Each `GetData` response transfers a separately owned STGMEDIUM to its consumer. Consumers release it with `ReleaseStgMedium`. No CF_HDROP/path list or disk fallback.
- Dedicated OLE-initialized STA thread owns a hidden dispatcher control and runs its own WinForms message pump. It is independent of any session window and of the visible probe control. Failed publication retains the same data object for Retry. Explicit stop checks `OleIsCurrentClipboard` before clearing its own offering; another application's replacement is preserved. There is no `OleFlushClipboard` or persistence after process exit.
- Opt-in command-line probe generates a synthetic 480×120 PNG showing `T02-IN-MEMORY-7319`, plus `instructions.txt`, in one ZIP named `gaze-transcribe-probe.zip`. PNG and ZIP are constructed with in-memory streams. The marker is absent from instructions, allowing a human to distinguish PNG reading from instructions-only reading.
- `src/Gaze.App/Program.cs`: exact one-argument launch and stop switches only; ordinary application startup does not publish probe data.
- `tests/Gaze.Clipboard.Tests`: standalone executable harness links the same producer/owner source. Real clipboard publication is never used by this harness. Publication/current-owner/clear delegates are injected only for failure and stop checks; native IStream, COM marshaling and the pumping STA are real.

Source review found no file creation/write, temp path, file-drop, flush or fallback operations in the probe. `Bitmap.Save` receives a ZIP entry stream, not a path. Build outputs and this textual evidence are intentional repo artifacts; fixture PNG/ZIP bytes were never saved. **Live producer file-write tracing during actual clipboard publication has not been performed**; source review and isolated tests do not substitute for that observation.

Platform contracts checked against [Shell clipboard formats](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard), [FILEGROUPDESCRIPTORW](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/ns-shlobj_core-filegroupdescriptorw), [FILEDESCRIPTORW](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/ns-shlobj_core-filedescriptorw), [Shell data objects](https://learn.microsoft.com/en-us/windows/win32/shell/dataobject) and [OleSetClipboard](https://learn.microsoft.com/en-us/windows/win32/api/ole2/nf-ole2-olesetclipboard). These describe the producer contract and delayed rendering; they do not establish Claude support.

## Actual RED/GREEN evidence

Focused command:

```powershell
dotnet run --project tests/Gaze.Clipboard.Tests --no-restore -p:BuildInParallel=false -p:UseSharedCompilation=false -m:1
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

1. Behavioral RED, exit 1: compiling placeholder producer returned `DV_E_FORMATETC` for every request. The harness failed with `FAIL System.Exception: supported formats`.
2. Producer GREEN / ownership RED, exit 1: after implementing the descriptor/streams/fixture, the producer checks printed PASS, then the harness failed with `FAIL System.Exception: independent STA` because the placeholder owner executed on the caller thread.
3. GREEN, exit 0: after implementing the dedicated pumping STA, all original contract and ownership checks passed. Additional isolated native COM, failure/retry, ownership-stop, medium-negotiation and clipboard-sequence checks also passed.

Harness development also encountered an incorrect `IEnumFORMATETC.Clone` call signature, a ref-parameter lambda compile error, and an attempted `ReleaseComObject` on .NET's unwrapped managed CCW. These were corrected; they are **not** counted as behavioral RED. The cross-apartment check now invokes native IDataObject/IEnumFORMATETC vtables after COM inter-thread marshaling, avoiding same-process managed-object unwrapping.

Final harness output, exit 0:

```text
PASS isolated IDataObject descriptor, enumeration, immutable/repeated streams, rejected requests, ZIP and PNG decode
PASS marshaled MTA consumer retrieves data and enumeration from pumping STA after session-window disposal
PASS independent pumping STA survives transient window; no real clipboard mutation
PASS injected OLE failure retained/retried; stop preserves replacement clipboard and releases own offering
PASS real clipboard sequence unchanged
```

Assertions include descriptor count/name/size, exact format enumeration and reset/clone, immutable caller-byte snapshot, fresh readable streams, lindex/aspect/target-device/medium/unknown-format rejection with consistent HRESULTs, medium bitmask negotiation, ordered ZIP entries, PNG decoding and rendered pixels, instructions without the visible marker, a native marshaled MTA consumer after transient window disposal, injected `CLIPBRD_E_CANT_OPEN` surfaced with retained identical retry object/bytes, and injected current/replaced ownership on stop. These establish a producer/isolated-consumer contract, not an external receiver result or production batch workflow.

## Serial build and regression checks actually run

`scripts/Build.ps1` has no serial option and uses the default restore/build scheduling. To honor the requested serial execution without editing unrelated build tooling, its restore/build/core-harness steps were executed separately with serial flags:

```powershell
dotnet restore src/Gaze.App --packages .packages --disable-parallel -p:BuildInParallel=false -m:1
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build src/Gaze.App --no-restore -c Debug -p:Platform=x64 -p:BuildInParallel=false -p:UseSharedCompilation=false -m:1 -o artifacts/app
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project tests/Gaze.Tests -p:BuildInParallel=false -p:UseSharedCompilation=false -m:1
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build src/Gaze.App --no-restore -c Release -p:Platform=x64 -p:BuildInParallel=false -p:UseSharedCompilation=false -m:1 -o artifacts/clipboard-release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project tests/Gaze.Settings.Tests -p:BuildInParallel=false -p:UseSharedCompilation=false -m:1
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
py -3 -m unittest discover -s tests -p test_timeline_reader.py
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
git diff --check
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Results: restore exit 0; final Debug and Release builds exit 0 with 0 warnings/0 errors; core harness 41 PASS checks; settings harness PASS; Python 5 tests OK. The prescribed `python` command failed because it resolves to the Microsoft Store alias; the existing `py -3` launcher successfully ran the same test discovery. `Build.ps1` itself was not invoked. No builds ran concurrently. The focused clipboard harness remains an explicit additional command, not part of the existing build script.

## Exact parent launch and stop instructions

Run from the repo root after reviewing the code/evidence. **Do not run the launch command until the parent explicitly chooses to replace the current clipboard.** Clipboard preservation applies until launch; launch intentionally replaces it and stop does not restore the previous contents. No ZIP/PNG paths or evidence-directory arguments are needed.

Launch the built Debug executable (displays the probe status/Retry/Stop control):

```powershell
& .\artifacts\app\Gaze.App.exe --transcribe-clipboard-probe
```

Confirm the control reports `Offered gaze-transcribe-probe.zip in memory`. If it reports failure, Retry republishes retained bytes; no file is saved. Keep the process running during paste. Minimize the probe control to test the independent owner's lifetime; closing the probe control intentionally stops the probe. The automated disposed-window check uses a separate transient window, not this stop control.

Stop via the control's **Stop probe** button, its close button, or from a second PowerShell:

```powershell
$stop = Start-Process -FilePath .\artifacts\app\Gaze.App.exe -ArgumentList '--stop-transcribe-clipboard-probe' -WindowStyle Hidden -Wait -PassThru
if ($stop.ExitCode -ne 0) { throw 'No running clipboard probe stop event was found.' }
```

The named stop event is polled every 200 ms; command exit 0 acknowledges signaling, not completed shutdown. Observe the probe process/control exit. Graceful stop clears the offering only when it remains this producer's clipboard object. If another app has replaced the clipboard, that replacement is preserved. Terminating the process without graceful stop is a separate process-exit limitation test, not a persistence guarantee. A second probe launch is rejected while one is active. Stop does not quit ordinary Gaze tray instances.

## Suggested human-only paste/ingestion validation

Record observations as text here; do not save screenshots, fixture ZIPs or temp staging files. No automated Claude interaction or message sending is authorized/performed.

1. Record the installed Claude Desktop version and Windows build. Launch the probe explicitly as above and verify its offered status. If file activity monitoring is available, observe the producer process for PNG/ZIP/temp writes; record the tool, process filter, interval and actual result. This runtime no-spill observation remains pending.
2. In Claude Desktop, focus a new unsent conversation's attachment/input area and press Ctrl+V. Record whether `gaze-transcribe-probe.zip` appears as an attachment, whether it is rejected, or whether nothing happens. An attachment chip alone is not ingestion evidence.
3. Minimize the probe control, retain the same running owner process, remove the draft attachment and paste again. Record delayed rendering/lifetime behavior separately. Do not infer session-window compatibility solely from this control; the isolated disposed-window test is the currently verified window-lifetime contract.
4. Only the human may choose to submit a message to test actual ingestion. Suggested text: "Read instructions.txt in the attached ZIP and transcribe the text in 000001_probe.png. If you cannot open the ZIP or read the image, say so." Expected image text is `T02-IN-MEMORY-7319`. Record the actual response and whether the PNG was read, separate from paste acceptance; do not supply that expected text in the message.
5. Stop the probe. Record what remains available before any new copy. Separately, relaunch, copy harmless text in another application, then stop; verify the replacement text is still available. For the accepted process-exit limitation, optionally relaunch and terminate the owner before paste, recording the observed behavior without requiring persistence.
6. If paste or ingestion is unsupported, record the exact result and reopen the export design with the user before dependent production integration. Do not use a staged disk ZIP, manual upload from disk or other fallback to turn this test into a pass.

| Evidence | Actual status |
| --- | --- |
| In-memory producer / isolated consumer contract | PASS |
| Pumping independent STA / disposed transient window | PASS |
| Injected publication failure, retention and Retry | PASS |
| Injected stop ownership decisions | PASS |
| Automated real clipboard sequence preservation | PASS |
| Real clipboard publication / delayed rendering | Pending parent opt-in launch |
| Claude Desktop version, paste attachment and ZIP/PNG ingestion | Pending human validation |
| Live clipboard replacement/stop/process-exit behavior | Pending human validation |
| Runtime producer file-write observation | Pending human validation |

No external destination is listed as compatible. AC11 native acceptance and T02 completion remain pending. AC13 evidence here covers probe-level retained publication failure; production retained-batch integration belongs to later approved tasks and was not implemented.

## Human Claude Desktop paste result (2026-10-14)
The human launched the probe and reported Ctrl+V did nothing in Claude Desktop. Their attached screenshot shows the probe status exactly: "Offered gaze-transcribe-probe.zip in memory." Thus the probe reports successful publication, while the observed Claude Desktop paste produced no attachment. ZIP ingestion could not be tested. This establishes failure of the intended paste workflow for the current probe/environment, not universal rejection of all virtual files or proof that every producer detail is correct. Current Claude version was not reverified. T02 remains incomplete; no production export integration and no disk fallback authorized. Reopen export design or diagnose receiver/producer interoperability before advancing dependent export tasks.

## Human-approved clipboard export revision (2026-10-14)
After virtual in-memory ZIP publication succeeded but manual Ctrl+V in Claude Desktop produced no attachment, the human said exactly "temp storrage is acceptable" in response to using a temporary ZIP file copied to the clipboard. This explicitly supersedes the no-disk/no-temp-spill clipboard constraints for temporary ZIP staging only. Keep AutoCopy-on without a permanent video-folder save, keep AutoCopy-off saving to video folder, and retain failure/Retry behavior. Prefer building PNG/ZIP bytes in memory, writing only the final ZIP into an app-owned temp location, and publishing a conventional file-drop path. Test actual Claude paste acceptance and ZIP ingestion separately; neither is assumed. Keep the staged file available while pasting; design/document cleanup without deleting unrelated files or currently offered files. No automatic cloud upload or messaging authorized.

## Revised T02: temporary final ZIP file-drop probe (2026-10-14)

Approved implementation work is complete; manual Claude paste acceptance and ZIP/PNG ingestion remain pending, with no compatibility claim. Native app control is unavailable and is not an implementation prerequisite. No real probe was launched, no actual clipboard mutation or Claude messaging occurred in this continuation. Previous virtual probe, tests and evidence are preserved. Factory completion IDs remain unchanged; no production integration, later tasks or commits.

### Implementation and lifetime

`src/Gaze.App/TranscribeFileDrop.cs` adds a separate exact one-argument opt-in. Ordinary startup and the existing virtual probe are unchanged. PNG and ZIP are built in memory using the original fixture (image marker `T02-IN-MEMORY-7319`, absent from instructions). Only the final ZIP is written, with CreateNew and flushed/closed before publication, at:

`%TEMP%\Gaze.TranscribeClipboardProbe\gaze-transcribe-<32-character GUID>\gaze-transcribe-probe.zip`

No configured video-folder copy or standalone PNG is written. Each instance gets a new directory; there is no old-instance sweep. The conventional file-drop uses [WinForms DataObject.SetFileDropList](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.dataobject.setfiledroplist?view=windowsdesktop-10.0) and OLE publication on the independent pumping STA. This contract does not establish receiver support.

Partial staging failure attempts exact-file cleanup before Retry; partial bytes are never published. Memory bytes remain retained. Publication failure retains the complete staged ZIP and data object; Retry reuses both without rewriting. If partial cleanup fails, publication stays disabled; Stop must clean up before starting a fresh probe. No permanent-save fallback exists.

The file remains available until explicit graceful Stop/close. Minimize preserves availability. Stop checks ownership even after failed publication, clears only its own offering, then deletes the exact staged ZIP and removes the instance directory only if empty. Another owner's replacement is preserved; if its conventional file-drop still references this ZIP, Stop refuses deletion until the human copies other content. Ownership/query/clear failures retain file and process. A deletion failure after withdrawal retains the file for Stop retry without clearing a later clipboard. Stop failures cancel window close and display reason/path; retry Stop after resolving the issue. Successful Stop is idempotent; Retry after withdrawal/stop is rejected. Reparse-point directories/ancestors/files are refused. No recursive deletion, unrelated sibling deletion, shared-root deletion or old-instance cleanup occurs.

Consumers must finish reading before Stop; the probe cannot determine when Claude finishes ingestion. Forced termination/crash may leave an orphan ZIP; no automatic orphan cleanup or persistence guarantee exists. For manual orphan cleanup, first confirm the probe has exited and copy harmless unrelated content to replace any remaining file-drop. Delete only the exact displayed ZIP using `Remove-Item -LiteralPath '<exact displayed ZIP path>'`; remove its GUID directory only if empty using `Remove-Item -LiteralPath '<exact displayed GUID directory>'` without `-Recurse`. Never use wildcard/shared-root cleanup. Leave files whose ownership is uncertain.

### Exact manual launch, retry and stop

From repository root, after explicitly choosing to replace the current clipboard:

```powershell
& .\artifacts\app\Gaze.App.exe --transcribe-file-drop-probe
```

Launch replaces the clipboard; previous contents are not restored. Confirm `Offered temporary ZIP file-drop` and note the displayed full path. If Offer fails, press **Retry**. Keep process/file available during paste and ingestion. A second file-drop probe instance is rejected. Do not run the old virtual probe simultaneously.

Stop with **Stop probe**, close, or from another PowerShell:

```powershell
$probeStop = Start-Process -FilePath .\artifacts\app\Gaze.App.exe -ArgumentList '--stop-transcribe-file-drop-probe' -WindowStyle Hidden -Wait -PassThru
if ($probeStop.ExitCode -ne 0) { throw 'No running file-drop probe stop event was found.' }
```

Exit 0 acknowledges signaling, not completed cleanup. The event is polled every 200 ms. Confirm control/process exit and disappearance of the exact staged ZIP. On Stop failure the control remains open; resolve the displayed issue and press Stop or signal again. Stop does not affect ordinary Gaze or the old virtual probe; its `--stop-transcribe-clipboard-probe` switch does not stop this new probe.

### Human-only Claude verification (pending)

1. Record Windows build and installed Claude Desktop version. Explicitly launch this new probe and record offered status/path. Optional process file tracing should show final ZIP staging only, without PNG/video-folder copy; runtime tracing remains unperformed.
2. Focus Claude's unsent conversation input/attachment area and press Ctrl+V. Record attachment, rejection or nothing. This establishes paste acceptance only.
3. Minimize the probe and repeat draft paste to observe lifetime. Keep it running until all desired ingestion checks finish.
4. Only the human may choose to submit an ingestion message. Suggested text: “Read instructions.txt in the attached ZIP and transcribe the text in 000001_probe.png. If you cannot open the ZIP or read the image, say so.” Do not supply the expected image marker. Record ZIP access and PNG text reading separately from attachment acceptance.
5. After ingestion finishes, Stop and verify exact-file cleanup. Separately relaunch, copy harmless text elsewhere, Stop, and verify that replacement survives. If a replacement file-drop references this ZIP, copy other content before retrying Stop.
6. Record failures as failures. Publication success, native CF_HDROP retrieval and attachment chips do not establish ingestion or compatibility. No dependent production integration on assumed support.

### Revised RED/GREEN and serial checks actually run

The focused serial command remains the clipboard harness command above. Test storage is isolated under `artifacts/clipboard-tests/<GUID>`. Injected publication/ownership/clear/reference callbacks prevent actual clipboard publication. Native CF_HDROP HGLOBAL is retrieved and decoded with DragQueryFile on the independent OLE STA, verifying its single path matches the readable staged ZIP. Existing virtual/native COM checks remain intact.

- Initial compilable placeholder RED: exit 1 at StagedZipOffer.Retry with NotImplementedException. A preceding ambiguous IDataObject compile error was corrected and is not behavioral RED.
- Behavioral staging RED: temporarily omitted partial cleanup; exit 1 with `partial stage removed; no publication`.
- Behavioral publication RED: temporarily ignored publication HRESULT; exit 1 with `publication error missing`.
- Mutations were immediately restored in finally blocks. Final GREEN: exit 0, all previous and new checks PASS, real clipboard sequence unchanged. Failed mutation runs may leave deliberately isolated artifacts in unique test directories; these are not actual clipboard offerings or production temp staging.

Assertions cover partial write/no publication, immutable snapshot, complete ZIP retained after publication failure, same object/path retry without rewrite, native CF_HDROP count/path, readable ZIP, ownership failure retention, clear failure retention, locked-file cleanup failure/retry after withdrawal, idempotent/unused Stop, stopped Retry rejection, replacement clipboard preservation, same-path replacement retention, and unrelated root/sibling files preserved.

Serial restore and Debug/Release commands are those recorded above: Debug `artifacts/app`, Release `artifacts/clipboard-release`. Restore uses `--disable-parallel -p:BuildInParallel=false -m:1`; builds use `--no-restore -p:BuildInParallel=false -p:UseSharedCompilation=false -m:1`, one at a time. Final Debug/Release: exit 0, 0 warnings/0 errors. Core: 41 PASS; settings: PASS; `py -3` timeline tests: 5 OK. Final clipboard harness: all PASS, real clipboard unchanged. `git diff --check`: exit 0. Build.ps1 steps were reproduced serially without editing unrelated build tooling.
