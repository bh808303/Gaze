namespace Gaze.Core;

public sealed record TimelineEvent(int Frame, int RecordingFrame, DateTimeOffset ClockTime, string Kind,
    string? Process = null, string? Title = null, string? WindowId = null, string? Button = null,
    int? ScreenX = null, int? ScreenY = null, int? X = null, int? Y = null, bool CarryOver = false, string? Scope = null, bool External = false,
    int? VirtualKey = null, int? ScanCode = null, string? Key = null, string? KeyState = null, bool? Repeat = null, bool? Extended = null, bool? Injected = null);
public sealed record ClockAnchor(int Frame, int RecordingFrame, DateTimeOffset ClockTime, string Reason, DateTimeOffset? TransitionTime = null);
public sealed record TimelinePart(int Version, int FramesPerSecond, int FrameCount, int RecordingFirstFrame,
    DateTimeOffset RecordingStartTime, DateTimeOffset PartStartTime, IReadOnlyList<ClockAnchor> ClockAnchors, IReadOnlyList<TimelineEvent> Events);

public sealed record TimelineSnapshot(int FramesPerSecond, IReadOnlyList<DateTimeOffset> FrameTimes,
    IReadOnlyList<ClockAnchor> Anchors, IReadOnlyList<TimelineEvent> Events)
{
    public TimelinePart Slice(int first, int count)
    {
        if (first < 0 || count < 1 || first > FrameTimes.Count - count) throw new ArgumentOutOfRangeException(nameof(first));
        var events = Events.Where(e => e.Frame >= first && e.Frame < first + count)
            .Select(e => e with { Frame = e.Frame - first }).ToList();
        var preceding = Events.Where(e => e.Kind == "window" && e.Frame < first).GroupBy(e => e.Scope)
            .Select(g => g.Last()).Where(e => !events.Any(current => current.Kind == "window" && current.Frame == 0 && current.Scope == e.Scope));
        events.InsertRange(0, preceding.Select(e => e with { Frame = 0, CarryOver = true }));
        if (events.Count == 0) events.Add(new(0, first, FrameTimes[first], "part-start"));
        var anchors = Anchors.Where(a => a.Frame >= first && a.Frame < first + count)
            .Select(a => a with { Frame = a.Frame - first }).ToList();
        if (anchors.All(a => a.Frame != 0)) anchors.Insert(0, new(0, first, FrameTimes[first], "part-start"));
        // LINQ ordering is stable: equal frame/clock keys retain processing order.
        var orderedEvents = events.OrderBy(e => e.Frame).ThenBy(e => e.ClockTime).ToArray();
        return new(1, FramesPerSecond, count, first, FrameTimes[0], FrameTimes[first], anchors, orderedEvents);
    }
}

// Hooks and the encoder share this small, locked timeline. Video positions are
// sampled-frame indices; event clock times retain the independent hook timestamps.
public sealed class RecordingTimeline
{
    private readonly object gate = new();
    private readonly int fps;
    private readonly List<DateTimeOffset> frames = [];
    private readonly List<ClockAnchor> anchors = [];
    private readonly List<TimelineEvent> events = [];
    private readonly List<TimelineEvent> resumedInputs = [];
    private readonly Dictionary<string, TimelineEvent> windows = [];
    private DateTimeOffset? resume;
    private bool active, closed;
    public RecordingTimeline(int fps)
    {
        if (fps is < 1 or > 30) throw new ArgumentOutOfRangeException(nameof(fps));
        this.fps = fps;
    }
    private int CurrentFrame => Math.Max(0, frames.Count - 1);
    public void Frame(DateTimeOffset time)
    {
        lock (gate)
        {
            if (closed) return;
            int frame = frames.Count; frames.Add(time);
            if (frame == 0 || resume.HasValue)
            {
                anchors.Add(new(frame, frame, time, frame == 0 ? "start" : "resume", resume ?? time));
                if (frame == 0) events.Add(new(0, 0, time, "start"));
                if (resume.HasValue) events.Add(new(frame, frame, resume.Value, "resume"));
                foreach (var window in windows.Values)
                {
                    var previousWindow = events.LastOrDefault(e => e.Kind == "window" && e.Scope == window.Scope);
                    if (previousWindow == null || previousWindow.External != window.External || previousWindow.WindowId != window.WindowId || previousWindow.Process != window.Process || previousWindow.Title != window.Title)
                        events.Add(window with { Frame = frame, RecordingFrame = frame, ClockTime = time });
                }
                resume = null;
                events.AddRange(resumedInputs.Select(e => e with { Frame = frame, RecordingFrame = frame }));
                resumedInputs.Clear();
            }
            active = true;
        }
    }
    public void Window(string? process, string? title, string? id, DateTimeOffset time, string scope = "foreground", bool external = false)
    {
        lock (gate)
        {
            if (external) process = title = id = null;
            windows.TryGetValue(scope, out var previous);
            if (closed || (previous != null && previous.External == external && previous.Process == process && previous.Title == title && previous.WindowId == id)) return;
            var window = new TimelineEvent(CurrentFrame, CurrentFrame, time, "window", process, title, id, Scope: scope, External: external);
            windows[scope] = window;
            if (active) events.Add(window);
        }
    }
    public void Click(string button, int screenX, int screenY, int x, int y, DateTimeOffset time)
    {
        lock (gate)
        {
            if (closed) return;
            var click = new TimelineEvent(CurrentFrame, CurrentFrame, time, "click", Button: button, ScreenX: screenX, ScreenY: screenY, X: x, Y: y);
            if (active) events.Add(click);
            else if (resume.HasValue) resumedInputs.Add(click);
        }
    }
    public void Pause(DateTimeOffset time)
    {
        lock (gate)
        {
            if (closed) return;
            if (!active)
            {
                // A second pause before a resumed sample cancels that unsampled interval.
                resume = null; resumedInputs.Clear(); return;
            }
            events.Add(new(CurrentFrame, CurrentFrame, time, "pause")); active = false;
        }
    }
    public void Keyboard(int virtualKey, int scanCode, string key, bool down, bool repeat, bool extended, bool injected, DateTimeOffset time)
    {
        lock (gate)
        {
            if (closed) return;
            var input = new TimelineEvent(CurrentFrame, CurrentFrame, time, "key", VirtualKey: virtualKey, ScanCode: scanCode, Key: key,
                KeyState: down ? "down" : "up", Repeat: repeat, Extended: extended, Injected: injected);
            if (active) events.Add(input);
            else if (resume.HasValue) resumedInputs.Add(input);
        }
    }
    public void Resume(DateTimeOffset time) { lock (gate) { if (!closed) resume = time; } }
    public void Stop() { lock (gate) { active = false; closed = true; resumedInputs.Clear(); } }
    public TimelineSnapshot Snapshot(int frameCount)
    {
        lock (gate)
        {
            if (frameCount < 1 || frameCount > frames.Count) throw new ArgumentOutOfRangeException(nameof(frameCount));
            return new(fps, frames.Take(frameCount).ToArray(), anchors.Where(a => a.Frame < frameCount).ToArray(), events.Where(e => e.Frame < frameCount).ToArray());
        }
    }
}
