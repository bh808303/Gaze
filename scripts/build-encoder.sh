#!/usr/bin/env bash
# Run from the encoder-build workspace using Git Bash. See installer/ENCODER.md.
set -euo pipefail
root="$PWD"
# Prevent upstream version detection from picking up Gaze's enclosing Git repo.
export revision=7.1
export PATH="$root/toolchain/llvm-mingw-20261006-ucrt-x86_64/bin:$root/nasm/nasm-2.16.03:$root/make/usr/bin:$PATH"
mkdir -p "$root/install" "$root/output"
cd "$root/sources/x264"
./configure --host=x86_64-w64-mingw32 --cross-prefix=x86_64-w64-mingw32- \
  --prefix="$root/install" --enable-static --disable-cli --disable-opencl --enable-win32thread
make -j4
make install
# Only x264 is enabled as an external dependency. This replaces pkg-config
# for its single, static, native Win32-thread library; no other libraries.
cat > "$root/x264-pkg-config" <<EOF
#!/usr/bin/env bash
case "\$*" in
  *--cflags*) echo '-I$root/install/include' ;;
  *--libs*) echo '-L$root/install/lib -lx264' ;;
  *--modversion*) echo '0.164.3108' ;;
  *--exists*|*--version*) exit 0 ;;
  *) exit 1 ;;
esac
EOF
chmod +x "$root/x264-pkg-config"
cd "$root/sources/ffmpeg"
./configure --target-os=mingw32 --arch=x86_64 --enable-cross-compile \
  --cross-prefix=x86_64-w64-mingw32- --pkg-config="$root/x264-pkg-config" \
  --prefix="$root/install" --enable-gpl --enable-version3 --enable-libx264 \
  --disable-autodetect --disable-everything --disable-doc --disable-debug \
  --disable-ffplay --disable-ffprobe --enable-ffmpeg \
  --enable-protocol=file,pipe --enable-demuxer=rawvideo --enable-decoder=rawvideo \
  --enable-encoder=libx264 --enable-muxer=mp4 \
  --enable-filter=scale,format,null,anull
make -j4 REVISION=7.1
cp ffmpeg.exe "$root/output/ffmpeg.exe"
cp config.h ffbuild/config.mak "$root/output/"
"$root/output/ffmpeg.exe" -version > "$root/output/FFmpeg-build.txt"
