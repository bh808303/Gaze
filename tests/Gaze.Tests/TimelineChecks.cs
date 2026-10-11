using Gaze.Core;

internal static class TimelineChecks
{
    internal static void Run(Action<string, Action> test)
    {
        var start = new DateTimeOffset(2026, 10, 11, 9, 0, 0, TimeSpan.FromHours(2));
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
