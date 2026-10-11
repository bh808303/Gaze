"""Verify decoded synthetic fixture footage, not just successful file creation."""
import json
import subprocess
import sys
from pathlib import Path

folder, ffmpeg = Path(sys.argv[1]), sys.argv[2]
result = json.loads((folder / "result.json").read_text(encoding="utf-8-sig"))
assert result["success"], result["detail"]
assert not (folder / "unhandled.txt").exists(), "Fixture shutdown raised an exception"
width, height = result["canvas"]["Width"], result["canvas"]["Height"]
video = next(folder.glob("Gaze*.mp4"))
decoded = subprocess.run([ffmpeg, "-hide_banner", "-i", str(video), "-vf", "fps=1", "-f", "rawvideo", "-pix_fmt", "rgb24", "pipe:1"], capture_output=True, check=True)
(folder / "decode.txt").write_bytes(decoded.stderr)
frame_bytes = width * height * 3
frames = [decoded.stdout[i:i + frame_bytes] for i in range(0, len(decoded.stdout), frame_bytes)]
assert len(frames) >= 8 and all(len(f) == frame_bytes for f in frames), "Missing or incomplete decoded frames"
classes = []
for frame in frames:
    samples = [frame[(y * width + x) * 3:(y * width + x) * 3 + 3] for y in range(0, height, 8) for x in range(0, width, 8)]
    def fraction(rgb):
        return sum(all(abs(a - b) <= 15 for a, b in zip(p, rgb)) for p in samples) / len(samples)
    assert fraction((255, 0, 255)) < 0.01, "Unrelated covering window leaked into capture"
    assert fraction((0, 0, 0)) < 0.95, "Black-frame regression"
    classes.append("dialog" if fraction((215, 245, 220)) > 0.1 else "root" if fraction((240, 245, 250)) > 0.5 else "unknown")
assert classes[0] == "root", classes
assert "dialog" in classes, "Owned dialog was not captured"
assert classes[-1] == "root", "Capture did not return to original window"
assert "Video: h264" in decoded.stderr.decode(errors="replace"), "Unexpected video codec"
assert "Audio:" not in decoded.stderr.decode(errors="replace"), "Unexpected audio stream"
print(json.dumps({"success": True, "decoded_frames": len(frames), "sequence": classes, "bytes": video.stat().st_size}))
