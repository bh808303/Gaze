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
    def test_filters_keep_collection_and_geometry_context(self):
        timeline = {"collection": {"recordTimelineMetadata": True}, "collectionStatus": [{"state": "lost", "lost": 2}],
                    "events": [{"kind": "geometry", "frame": 0, "clockTime": "2026-10-13T12:00:00Z", "scope": "captured"},
                               {"kind": "pointer", "frame": 2, "clockTime": "2026-10-13T12:00:02Z"},
                               {"kind": "wheel", "frame": 3, "clockTime": "2026-10-13T12:00:03Z"}]}
        result = reader.filter_timeline(timeline, kinds=["wheel"], first_frame=3, last_frame=3,
                                        after="2026-10-13T12:00:03+00:00", before="2026-10-13T12:00:03Z")
        self.assertEqual([e["kind"] for e in result["events"]], ["wheel"])
        self.assertEqual(result["collection"], timeline["collection"])
        self.assertEqual(result["collectionStatus"], timeline["collectionStatus"])
        self.assertEqual(result["context"][0]["kind"], "geometry")

    def test_state_context_independent_of_kind_and_inclusive_bounds(self):
        events = [{"kind": kind, "scope": scope, "frame": frame,
                   "clockTime": f"2026-10-13T12:00:{frame:02d}Z"}
                  for kind, scope, frame in [("geometry", "monitor", 0), ("window", "captured", 0),
                      ("pointer-boundary", None, 0), ("geometry", "monitor", 2),
                      ("window", "captured", 2), ("pointer-boundary", None, 2),
                      ("wheel", None, 3), ("geometry", "monitor", 4)]]
        for bounds, indexes in [({}, [0,1,2,3,4,5,7]),
                ({"first_frame":0,"last_frame":3}, [0,1,2,3,4,5]),
                ({"first_frame":2,"last_frame":3}, [0,1,2,3,4,5]),
                ({"first_frame":1,"last_frame":4,"after":"2026-10-13T12:00:02Z",
                  "before":"2026-10-13T12:00:03Z"}, [0,1,2,3,4,5])]:
            with self.subTest(bounds=bounds):
                result=reader.filter_timeline({"events":events},kinds=["wheel"],**bounds)
                self.assertEqual(result["events"],[events[6]])
                self.assertEqual(result["context"],[events[i] for i in indexes])

    def test_combined_constraints_exclude_state_beyond_either_upper_bound(self):
        events=[{"kind":"geometry","scope":"monitor","frame":frame,
                 "clockTime":f"2026-10-13T12:00:{second:02d}Z"}
                for frame,second in [(0,0),(1,9),(9,1)]]
        result=reader.filter_timeline({"events":events},kinds=["wheel"],first_frame=2,last_frame=3,
                  after="2026-10-13T12:00:02Z",before="2026-10-13T12:00:03Z")
        self.assertEqual(result["context"],[events[0]])

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
