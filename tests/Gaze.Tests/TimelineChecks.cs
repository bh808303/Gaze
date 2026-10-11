using Gaze.Core;

internal static class TimelineChecks
{
    internal static void Run(Action<string, Action> test)
    {
        var start = new DateTimeOffset(2026, 10, 11, 9, 0, 0, TimeSpan.FromHours(2));
        test("Ownerless popup acceptance requires a recorded native menu owner or exact recorded combo list", () =>
        {
            Require(Gaze.App.TimelineBoundary.IncludesPopup("#32768", true, false), "Recorded native menu rejected");
            Require(Gaze.App.TimelineBoundary.IncludesPopup("ComboLBox", false, true), "Recorded combo list rejected");
            Require(!Gaze.App.TimelineBoundary.IncludesPopup("#32768", false, true), "Unrelated menu accepted by combo relationship");
            Require(!Gaze.App.TimelineBoundary.IncludesPopup("ComboLBox", true, false), "Unrelated combo accepted by menu relationship");
            Require(!Gaze.App.TimelineBoundary.IncludesPopup("UnrelatedWindow", true, true), "Same-thread arbitrary window accepted");
            Require(!Gaze.App.TimelineBoundary.IncludesPopup("#32768", false, false), "Foreign menu accepted");
        });
        test("Location events ignore window capture and child controls, refreshing monitor details only at boundary transitions", () =>
        {
            Require(!Gaze.App.TimelineBoundary.HandlesLocation(CaptureMode.Window, 1, 1, 1, 0, 0), "Window-mode movement was handled");
            Require(!Gaze.App.TimelineBoundary.HandlesLocation(CaptureMode.CurrentMonitor, 2, 1, 1, 0, 0), "Child-control movement was handled");
            Require(!Gaze.App.TimelineBoundary.HandlesLocation(CaptureMode.CurrentMonitor, 1, 1, 3, 0, 0), "Background movement was handled");
            Require(!Gaze.App.TimelineBoundary.HandlesLocation(CaptureMode.CurrentMonitor, 1, 1, 1, -4, 0), "Non-window object movement was handled");
            Require(Gaze.App.TimelineBoundary.HandlesLocation(CaptureMode.AllMonitors, 1, 1, 1, 0, 0), "Foreground root movement was ignored");
            var cache = new Gaze.App.MonitorBoundaryCache(); cache.Remember(1, true); int refreshes = 0;
            void Move(nint hwnd, bool inside)
            {
                if (!cache.Changed(hwnd, inside)) return;
                refreshes++; cache.Remember(hwnd, inside);
            }
            for (int i = 0; i < 120; i++) Move(1, true);
            Require(refreshes == 0, "Same-monitor movement refreshed window details");
            Move(1, false); for (int i = 0; i < 120; i++) Move(1, false);
            Move(1, true); Require(refreshes == 2, "Boundary flips were lost or repeatedly refreshed");
            Move(2, true); Require(refreshes == 3, "A different foreground window reused stale cache state");
        });
        test("Foreign window details are removed before storage and stay removed across pause and split carry-over", () =>
        {
            var timeline = new RecordingTimeline(5);
            timeline.Window("mail", "Recorded invoice", "1", start, "captured");
            timeline.Window("mail", "Recorded invoice", "1", start); timeline.Frame(start);
            timeline.Window("secret-process", "PRIVATE CUSTOMER TITLE", "secret-window-id", start.AddMilliseconds(20), external: true);
            timeline.Frame(start.AddMilliseconds(200)); timeline.Pause(start.AddMilliseconds(220));
            timeline.Window("another-secret", "ANOTHER SECRET", "another-id", start.AddSeconds(1), external: true);
            timeline.Resume(start.AddSeconds(2)); timeline.Frame(start.AddSeconds(2.2));
            var snapshot = timeline.Snapshot(3);
            Require(snapshot.Events.Where(e => e.External).All(e => e.Process == null && e.Title == null && e.WindowId == null), "Foreign details reached storage");
            foreach (var part in new[] { snapshot.Slice(0, 3), snapshot.Slice(1, 1), snapshot.Slice(2, 1) })
            {
                var json = System.Text.Json.JsonSerializer.Serialize(part);
                Require(!json.Contains("SECRET", StringComparison.OrdinalIgnoreCase) && !json.Contains("secret-window-id"), "Foreign details leaked into output");
                Require(part.Events.Last(e => e.Scope == "foreground").External, "External context did not replace the recorded foreground context");
                Require(part.Events.Any(e => e.Scope == "captured" && e.Title == "Recorded invoice"), "Recorded context was lost");
            }
        });
        test("Timeline boundary includes recorded relationships and overlapping monitor windows, excluding other monitors and gaps", () =>
        {
            var areas = new[] { new System.Drawing.Rectangle(-100, 0, 100, 100), new System.Drawing.Rectangle(100, 0, 100, 100) };
            Require(Gaze.App.TimelineBoundary.IncludesWindow(CaptureMode.Window, true, default, areas), "Recorded window or owned dialog was excluded");
            Require(!Gaze.App.TimelineBoundary.IncludesWindow(CaptureMode.Window, false, areas[0], areas), "Unrelated window was included by overlap");
            Require(Gaze.App.TimelineBoundary.IncludesWindow(CaptureMode.CurrentMonitor, false, new(-110, 0, 20, 20), new[] { areas[0] }), "Partly overlapping window was excluded");
            Require(!Gaze.App.TimelineBoundary.IncludesWindow(CaptureMode.CurrentMonitor, false, areas[1], new[] { areas[0] }), "Other-monitor window was included");
            Require(Gaze.App.TimelineBoundary.IncludesWindow(CaptureMode.AllMonitors, false, areas[1], areas), "Captured second monitor was excluded");
            Require(!Gaze.App.TimelineBoundary.IncludesPoint(new(50, 50), areas) && Gaze.App.TimelineBoundary.IncludesPoint(new(-50, 50), areas), "Monitor-gap click filtering failed");
        });
        test("Slice orders events by frame and clock time while retaining equal-time processing order", () =>
        {
            var timeline = new RecordingTimeline(5); timeline.Frame(start);
            timeline.Window("mail", "Dialog opened", "1", start.AddMilliseconds(40));
            timeline.Click("left", 1, 1, 1, 1, start.AddMilliseconds(20));
            timeline.Click("right", 2, 2, 2, 2, start.AddMilliseconds(40));
            timeline.Frame(start.AddMilliseconds(200));
            // Frame order takes precedence even if the system clock goes backwards.
            timeline.Click("middle", 3, 3, 3, 3, start.AddMilliseconds(10));
            var snapshot = timeline.Snapshot(2);
            var part = snapshot.Slice(0, 2);
            Require(part.Events.Select(e => e.Kind == "click" ? e.Button : e.Kind).SequenceEqual(new[] { "start", "left", "window", "right", "middle" }), "Chronology or equal-time stability was lost");
            var split = snapshot.Slice(1, 1);
            Require(split.Events[0].Button == "middle" && split.Events[1].CarryOver, "Split output was not clock ordered after carry-over insertion");
        });
        test("Clicks drained before the first resumed frame survive with original clocks and the resumed frame index", () =>
        {
            var timeline = new RecordingTimeline(5); var queue = new Gaze.App.ClickQueue();
            timeline.Frame(start); timeline.Pause(start.AddMilliseconds(100));
            timeline.Click("left", 0, 0, 0, 0, start.AddSeconds(1));
            timeline.Resume(start.AddSeconds(2));
            for (int i = 0; i < 5; i++) queue.Enqueue("left", new System.Drawing.Point(i, i), start.AddSeconds(2).AddMilliseconds(10 + i));
            queue.Drain((button, point, time) => timeline.Click(button, point.X, point.Y, point.X, point.Y, time));
            timeline.Frame(start.AddSeconds(2.2));
            var snapshot = timeline.Snapshot(2);
            var clicks = snapshot.Events.Where(e => e.Kind == "click").ToArray();
            Require(clicks.Length == 5, "Resume-gap clicks were dropped or paused clicks retained");
            for (int i = 0; i < clicks.Length; i++)
                Require(clicks[i].Frame == 1 && clicks[i].RecordingFrame == 1 && clicks[i].ClockTime == start.AddSeconds(2).AddMilliseconds(10 + i), "Click clock or resumed frame was altered");
            Require(snapshot.Slice(0, 1).Events.All(e => e.Kind != "click") && snapshot.Slice(1, 1).Events.Count(e => e.Kind == "click") == 5, "Resume clicks crossed the split boundary");
        });
        test("Pause and resume map to recorded frames and retain exact clock times", () =>
        {
            var timeline = new RecordingTimeline(5);
            timeline.Window("mail", "Inbox", "1", start);
            timeline.Frame(start); timeline.Click("left", 100, 200, 10, 20, start.AddMilliseconds(30));
            timeline.Frame(start.AddMilliseconds(200)); timeline.Pause(start.AddMilliseconds(250));
            timeline.Click("left", 0, 0, 0, 0, start.AddSeconds(1));
            timeline.Resume(start.AddSeconds(10)); timeline.Frame(start.AddSeconds(10.1));
            var part = timeline.Snapshot(3).Slice(0, 3);
            Require(part.Events.Count(e => e.Kind == "click") == 1, "Paused click must be ignored");
            Require(part.ClockAnchors.Last().Frame == 2 && part.ClockAnchors.Last().ClockTime == start.AddSeconds(10.1), "Resume must use the actual next sampled frame");
            Require(part.ClockAnchors.Last().TransitionTime == start.AddSeconds(10), "Global resume clock time was lost");
            Require(part.Events.Single(e => e.Kind == "resume").ClockTime == start.AddSeconds(10), "Resume request clock time was lost");
            Require(part.Events.Single(e => e.Kind == "click").ClockTime == start.AddMilliseconds(30), "Precise click time was lost");
        });
        test("An unsampled resume cancelled by pause or stop does not leak buffered clicks into a later interval", () =>
        {
            var timeline = new RecordingTimeline(5); timeline.Frame(start); timeline.Pause(start.AddMilliseconds(100));
            timeline.Resume(start.AddSeconds(1)); timeline.Click("left", 1, 1, 1, 1, start.AddSeconds(1.01));
            timeline.Pause(start.AddSeconds(1.02)); timeline.Resume(start.AddSeconds(2)); timeline.Frame(start.AddSeconds(2.2));
            Require(timeline.Snapshot(2).Events.All(e => e.Kind != "click"), "Cancelled resume clicks leaked");
            timeline.Pause(start.AddSeconds(3)); timeline.Resume(start.AddSeconds(4));
            timeline.Click("left", 2, 2, 2, 2, start.AddSeconds(4.01)); timeline.Stop();
            Require(timeline.Snapshot(2).Events.All(e => e.Kind != "click"), "Unsampled stopped clicks leaked");
        });
        test("Rapid clicks are retained independently of the video frame rate", () =>
        {
            var timeline = new RecordingTimeline(1); timeline.Frame(start);
            for (int i = 0; i < 20; i++) timeline.Click("left", i, i, i, i, start.AddMilliseconds(i * 5));
            var part = timeline.Snapshot(1).Slice(0, 1);
            Require(part.Events.Count(e => e.Kind == "click") == 20, "Clicks were sampled or deduplicated");
        });
        test("Split timeline has local timestamps, exact start clock, and window carry-over", () =>
        {
            var timeline = new RecordingTimeline(5); timeline.Window("mail", "Invoice <42> & customer", "1", start);
            timeline.Frame(start); timeline.Frame(start.AddMilliseconds(210));
            timeline.Click("right", -20, 50, 0, 10, start.AddMilliseconds(250));
            timeline.Frame(start.AddMilliseconds(430)); timeline.Frame(start.AddMilliseconds(650));
            timeline.Window("browser", "Ticket", "2", start.AddMilliseconds(660));
            var snapshot = timeline.Snapshot(4); var left = snapshot.Slice(0, 2); var right = snapshot.Slice(2, 2);
            Require(left.Events.Count(e => e.Kind == "click") == 1 && right.Events.All(e => e.Kind != "click"), "An event crossed the split boundary");
            Require(right.PartStartTime == start.AddMilliseconds(430), "Part start was estimated from nominal FPS");
            Require(right.Events[0].CarryOver && right.Events[0].Frame == 0 && right.Events[0].Title == "Invoice <42> & customer", "Active window was not carried over");
            Require(right.Events.Last().Frame == 1 && right.Events.Last().RecordingFrame == 3, "Window change was not rebased");
        });
        test("Title changes create events, unchanged titles do not, and original titles remain", () =>
        {
            var timeline = new RecordingTimeline(5); timeline.Frame(start);
            timeline.Window("mail", "Alice's invoice", "1", start);
            timeline.Window("mail", "Alice's invoice", "1", start.AddMilliseconds(20));
            timeline.Window("mail", "Bob's invoice", "1", start.AddMilliseconds(40));
            Require(timeline.Snapshot(1).Slice(0, 1).Events.Count(e => e.Kind == "window") == 2, "Window titles were filtered or duplicated");
        });
        test("A part without input events still contains a timing marker", () =>
        {
            var timeline = new RecordingTimeline(5); timeline.Frame(start); timeline.Frame(start.AddMilliseconds(200));
            var part = timeline.Snapshot(2).Slice(1, 1);
            Require(part.Events.Single().Kind == "part-start" && part.Events.Single().Frame == 0, "Event track would be empty");
        });
        test("Captured and foreground context remain separate across pause and split boundaries", () =>
        {
            var timeline = new RecordingTimeline(5);
            timeline.Window("mail", "Invoice", "1", start, "captured");
            timeline.Window("browser", "Other app", "2", start); timeline.Frame(start);
            timeline.Pause(start.AddMilliseconds(50));
            timeline.Window("mail", "Updated invoice", "1", start.AddSeconds(1), "captured");
            timeline.Resume(start.AddSeconds(2)); timeline.Frame(start.AddSeconds(2.1)); timeline.Frame(start.AddSeconds(2.3));
            var part = timeline.Snapshot(3).Slice(2, 1);
            Require(part.Events.Single(e => e.Scope == "captured").Title == "Updated invoice", "Captured context was overwritten by foreground activity");
            Require(part.Events.Single(e => e.Scope == "foreground").Title == "Other app", "Foreground context was lost");
        });
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
