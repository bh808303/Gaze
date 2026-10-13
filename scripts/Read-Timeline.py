"""Read Gaze chapters, clock anchors, and event text in one FFprobe invocation."""
import argparse
import base64
import binascii
import json
from datetime import datetime
from pathlib import Path
import shutil
import subprocess
import sys


def packet_events(packet):
    data = bytearray()
    for line in packet.get("data", "").splitlines():
        if ":" in line:
            hex_data = line.split(":", 1)[1].lstrip().split("  ", 1)[0].replace(" ", "")
            data.extend(bytes.fromhex(hex_data))
    if len(data) < 2:
        raise ValueError("truncated timed-text packet")
    size = int.from_bytes(data[:2], "big")
    if not size:
        return []
    if len(data) < 2 + size:
        raise ValueError("truncated timed-text payload")
    text = data[2:2 + size].decode("utf-8")
    prefix = "gaze-base64-v1:"
    if text.startswith(prefix):
        encoded = "".join(text[len(prefix):].split())
        text = base64.b64decode(encoded, validate=True).decode("utf-8")
    payload = json.loads(text)  # Legacy recordings contain plain JSON.
    events = payload["events"]
    if not isinstance(events, list) or any(not isinstance(event, dict) for event in events):
        raise ValueError("invalid event list")
    return events


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
        try:
            events.extend(packet_events(packet))
        except (ValueError, KeyError, TypeError, binascii.Error) as error:
            print(f"Warning: skipped malformed Gaze event packet at {packet.get('pts_time', 'unknown')}s: {error}", file=sys.stderr)
    timeline["events"] = events
    timeline["chapters"] = [{"startSeconds": float(c["start_time"]), "endSeconds": float(c["end_time"]),
                             "title": c.get("tags", {}).get("title", "")} for c in media.get("chapters", [])]
    return timeline


def filter_timeline(timeline, kinds=None, first_frame=None, last_frame=None, after=None, before=None):
    def clock(value):
        return datetime.fromisoformat(value.replace("Z", "+00:00"))
    def in_range(event):
        return ((first_frame is None or event["frame"] >= first_frame)
                and (last_frame is None or event["frame"] <= last_frame)
                and (after is None or clock(event["clockTime"]) >= clock(after))
                and (before is None or clock(event["clockTime"]) <= clock(before)))
    result = dict(timeline)
    result["events"] = [e for e in timeline["events"] if in_range(e) and (not kinds or e["kind"] in kinds)]
    starting = {}
    transitions = []
    for index, event in enumerate(timeline["events"]):
        if event["kind"] not in ("geometry", "window", "pointer-boundary"):
            continue
        # Both upper constraints apply to context too, even when clocks and
        # frames disagree (for example after a wall-clock adjustment).
        if ((last_frame is not None and event["frame"] > last_frame)
                or (before is not None and clock(event["clockTime"]) > clock(before))):
            continue
        if in_range(event):
            transitions.append((index, event))
        else:
            preceding = ((first_frame is not None and event["frame"] < first_frame)
                         or (after is not None and clock(event["clockTime"]) < clock(after)))
            if preceding:
                starting[(event["kind"], event.get("scope"))] = (index, event)
    result["context"] = [event for _, event in sorted(list(starting.values()) + transitions)]
    return result


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("recording", type=Path)
    parser.add_argument("--probe", type=Path, help="FFprobe executable (defaults to adjacent ffprobe.exe or PATH)")
    parser.add_argument("--kind", action="append", help="Event kind (repeatable)")
    parser.add_argument("--first-frame", type=int)
    parser.add_argument("--last-frame", type=int)
    parser.add_argument("--after", help="Inclusive ISO 8601 clock time with UTC offset")
    parser.add_argument("--before", help="Inclusive ISO 8601 clock time with UTC offset")
    args = parser.parse_args()
    adjacent = Path(__file__).resolve().with_name("ffprobe.exe")
    probe = args.probe or (adjacent if adjacent.is_file() else shutil.which("ffprobe"))
    if not probe:
        parser.error("FFprobe not found. Use --probe <path-to-ffprobe.exe>.")
    try:
        print(json.dumps(filter_timeline(read_timeline(args.recording, probe), args.kind, args.first_frame, args.last_frame, args.after, args.before), ensure_ascii=False, separators=(",", ":")))
    except (OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        print(f"Cannot read Gaze timeline: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
