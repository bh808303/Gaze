import base64
import contextlib
import importlib.util
import io
import json
import sys
import textwrap
from pathlib import Path
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("reader", Path(__file__).resolve().parents[1] / "scripts/Read-Timeline.py")
reader = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reader)


def packet(text):
    value = text.encode("utf-8")
    value = len(value).to_bytes(2, "big") + value
    return {"stream_index": 1, "pts_time": "0.2", "data": "00000000: " + value.hex()}


class ReaderChecks(unittest.TestCase):
    def test_legacy_and_base64_titles(self):
        events = [{"title": "C:\\New folder\\notes.txt | Line\\nbreak | Line\\Nbreak | real\nnewline | <b> 😀"}]
        text = json.dumps({"version": 1, "events": events})
        encoded = base64.b64encode(text.encode()).decode()
        for payload in [text, "gaze-base64-v1:" + encoded, "gaze-base64-v1:" + "\n".join(textwrap.wrap(encoded, 80))]:
            self.assertEqual(reader.packet_events(packet(payload)), events)

    def test_bad_packets_warn_and_keep_later_events(self):
        good = packet('{"events":[{"kind":"click"}]}')
        media = {"format": {"tags": {"gaze_timeline": '{"version":1}'}},
                 "streams": [{"index": 1, "codec_name": "mov_text", "tags": {"handler_name": "Gaze events"}}],
                 "packets": [good, packet('{"events":["bad\\escape"]}'), packet("gaze-base64-v1:!"),
                             packet('{"events":{}}'), good]}
        with patch.object(reader.subprocess, "run") as run, contextlib.redirect_stderr(io.StringIO()) as warnings:
            run.return_value.stdout = json.dumps(media)
            result = reader.read_timeline("recording.mp4", "ffprobe")
            self.assertEqual(result["events"], [{"kind": "click"}] * 2)
            self.assertEqual(warnings.getvalue().count("skipped malformed"), 3)
            self.assertEqual(run.call_count, 1)


if __name__ == "__main__":
    unittest.main()
