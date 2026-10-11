"""Read Gaze chapters, clock anchors, and event text in one FFprobe invocation."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys


def read_timeline(recording, probe):
    result = subprocess.run(
        [str(probe), "-v", "error", "-show_format", "-show_chapters", "-show_streams",
         "-show_packets", "-select_streams", "s", "-show_data", "-of", "json", str(recording)],
        capture_output=True, text=True, encoding="utf-8", check=True,
    )
    media = json.loads(result.stdout)
    value = media.get("format", {}).get("tags", {}).get("gaze_timeline")
    if value is None:
        raise ValueError("This recording has no Gaze event timeline.")
    timeline = json.loads(value)
    indexes = {stream["index"] for stream in media.get("streams", [])
               if stream.get("codec_name") == "mov_text"
               and stream.get("tags", {}).get("handler_name") == "Gaze events"}
    events = []
    for packet in media.get("packets", []):
        if packet.get("stream_index") not in indexes:
            continue
        data = bytearray()
        for line in packet.get("data", "").splitlines():
            if ":" in line:
                hex_data = line.split(":", 1)[1].lstrip().split("  ", 1)[0].replace(" ", "")
                data.extend(bytes.fromhex(hex_data))
        if len(data) < 2:
            continue
        size = int.from_bytes(data[:2], "big")
        if size:
            payload = json.loads(data[2:2 + size].decode("utf-8"))
            events.extend(payload["events"])
    timeline["events"] = events
    timeline["chapters"] = [{"startSeconds": float(c["start_time"]), "endSeconds": float(c["end_time"]),
                             "title": c.get("tags", {}).get("title", "")} for c in media.get("chapters", [])]
    return timeline


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("recording", type=Path)
    parser.add_argument("--probe", type=Path, help="FFprobe executable (defaults to adjacent ffprobe.exe or PATH)")
    args = parser.parse_args()
    adjacent = Path(__file__).resolve().with_name("ffprobe.exe")
    probe = args.probe or (adjacent if adjacent.is_file() else shutil.which("ffprobe"))
    if not probe:
        parser.error("FFprobe not found. Use --probe <path-to-ffprobe.exe>.")
    try:
        print(json.dumps(read_timeline(args.recording, probe), ensure_ascii=False, separators=(",", ":")))
    except (OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        print(f"Cannot read Gaze timeline: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
