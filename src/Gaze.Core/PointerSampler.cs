namespace Gaze.Core;

// Consumer-side boundary decisions: no foreign identity or coordinates are retained.
public sealed class PointerSampler
{
    private bool? inside;
    private (int X, int Y)? position;
    private long? last;
    public IReadOnlyList<TimelineEvent> Sample(bool included, int screenX, int screenY, int x, int y,
        DateTimeOffset time, long milliseconds, bool held, bool collectPosition = true)
    {
        var events = new List<TimelineEvent>();
        if (inside != included)
        {
            events.Add(new(0, 0, time, "pointer-boundary", Boundary: included ? "enter" : "leave"));
            inside = included;
            position = null;
        }
        if (!included || !collectPosition) return events;
        if (position == (screenX, screenY) || (last.HasValue && milliseconds - last.Value < (held ? 34 : 100))) return events;
        last = milliseconds; position = (screenX, screenY);
        events.Add(new(0, 0, time, "pointer", ScreenX: screenX, ScreenY: screenY, X: x, Y: y));
        return events;
    }
    public void Reset() { inside = null; position = null; last = null; }
}
