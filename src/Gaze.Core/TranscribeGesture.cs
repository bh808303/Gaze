using System.Drawing;

namespace Gaze.Core;

public sealed record TranscribeSelection(bool IsWindow, nint Window, string Title, Rectangle Area);

// One gesture snapshots its starting window/title/monitor; a later gesture gets a
// new instance. The adapter supplies SystemInformation.DragSize in physical pixels.
public sealed class TranscribeGesture
{
    private readonly Point start;
    private readonly Rectangle threshold, monitor;
    private readonly nint window;
    private readonly string title;
    private bool finished, dragging;
    public TranscribeGesture(Point start, Size dragSize, nint window, string title, Rectangle monitor)
    {
        if (dragSize.Width <= 0 || dragSize.Height <= 0) throw new ArgumentOutOfRangeException(nameof(dragSize));
        if (monitor.Width <= 0 || monitor.Height <= 0 || !monitor.Contains(start)) throw new ArgumentOutOfRangeException(nameof(monitor));
        this.start = start; this.window = window; this.title = title; this.monitor = monitor;
        threshold = new Rectangle(start.X - dragSize.Width / 2, start.Y - dragSize.Height / 2, dragSize.Width, dragSize.Height);
    }
    public Rectangle Preview(Point current)
    {
        if (finished) return Rectangle.Empty;
        dragging |= !threshold.Contains(current);
        if (!dragging) return Rectangle.Empty;
        // Clamp endpoints before subtraction to keep rectangle dimensions bounded.
        int x = Math.Clamp(current.X, monitor.Left, monitor.Right);
        int y = Math.Clamp(current.Y, monitor.Top, monitor.Bottom);
        int left = Math.Min(start.X, x), top = Math.Min(start.Y, y);
        return Rectangle.FromLTRB(left, top, Math.Max(start.X, x), Math.Max(start.Y, y));
    }
    public TranscribeSelection? Complete(Point end)
    {
        if (finished) return null;
        var area = Preview(end);
        finished = true;
        if (!dragging) return window == 0 ? null : new(true, window, title, Rectangle.Empty);
        return area.Width > 0 && area.Height > 0 ? new(false, window, title, area) : null;
    }
    public void Cancel() => finished = true;
}
