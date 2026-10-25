# Bundled encoder corresponding source

Gaze's MSI includes a private FFmpeg 7.1 executable with a statically linked
x264 encoder. It is built from unmodified upstream sources, with GPL version
3 or later applying to the combined encoder. Gaze launches it as a separate
process. It accepts raw BGRA frames and writes H.264 MP4. Recording splitting
also decodes H.264 from MP4 and uses trim/setpts filters. Event metadata adds
FFmetadata/SRT input and mov_text subtitles; the bundle also includes FFprobe
for extracting the timeline. Rebuild with the updated scripts before packaging
these features. Other codecs and
network protocols are intentionally disabled.

The installer release provides `Gaze-0.7.0-alpha-encoder-source.zip` alongside
the MSI. It contains the full original FFmpeg and x264 source archives, their
licenses, these instructions, both build scripts, and the actual build log,
FFmpeg configuration files and version output. No source patches are used.
The download checksums are pinned in `Build-Encoder.ps1`. The source ZIP is
free to download from the same release as the executable.

## Build on Windows x64

Prerequisites: Git for Windows (Bash and its MSYS runtime), PowerShell, Windows
tar, curl, and an available temporary drive letter between W and Z. Run:

```powershell
./scripts/Build-Encoder.ps1
./scripts/Build-Installer.ps1
```

In the standalone encoder source ZIP, run only `scripts/Build-Encoder.ps1`;
building the MSI additionally requires the complete Gaze repository checkout.

The first script downloads and verifies these pinned inputs into the ignored
`.tools/encoder-build` workspace:

| Input | Version / revision |
| --- | --- |
| FFmpeg | n7.1 |
| x264 | 31e19f92f00c7003fa115047ce50978bc98c3a0d |
| LLVM-MinGW, UCRT x86_64 | 20261006 |
| NASM | 2.16.03, win64 |
| GNU Make, MSYS2 | 4.4.1-2 |

The compiler, assembler and make are general-purpose build tools. Windows
system DLLs and UCRT are supplied by Windows. The executable requires no
separate x264 or FFmpeg DLLs. The build temporarily maps its workspace to a
free drive letter to avoid spaces in upstream makefiles, and removes that
mapping on exit. GitDirectory can be supplied if Git is installed elsewhere.

`build-encoder.sh` contains the full configure and make commands. Its small
pkg-config substitute describes only the installed static x264 library with
native Win32 threads; no other external codec libraries are enabled.

To build using the release's source archives without downloading them again,
copy `ffmpeg-n7.1.tar.gz` and `x264.tar.gz` from the ZIP into
`.tools/encoder-build` before running the PowerShell script. Existing archives
are verified before use. Run from a fresh workspace when changing any source
or build options. Successful output is `.tools/encoder-build/output/ffmpeg.exe`.

Build instructions describe a functional rebuild, not a promise of byte-for-byte
identity across Windows environments. The shipped executable and source ZIP
have SHA256 hashes in the release's `SHA256SUMS.txt`.
