using System.Drawing;
using Gaze.Core;

namespace Gaze.App;

internal static class TimelineBoundary
{
    internal static bool IncludesWindow(CaptureMode mode, bool recordedRelationship, Rectangle bounds, IReadOnlyList<Rectangle> areas)
        => mode == CaptureMode.Window ? recordedRelationship : areas.Any(area => area.IntersectsWith(bounds));
    internal static bool IncludesPoint(Point point, IReadOnlyList<Rectangle> areas) => areas.Any(area => area.Contains(point));
    internal static bool HandlesLocation(CaptureMode mode, nint hwnd, nint root, nint foreground, int objectId, int childId)
        => mode != CaptureMode.Window && hwnd != 0 && hwnd == root && root == foreground && objectId == 0 && childId == 0;
}

internal sealed class MonitorBoundaryCache
{
    private nint lastWindow;
    private bool lastIncluded, known;
    internal bool Changed(nint hwnd, bool included) => !known || hwnd != lastWindow || included != lastIncluded;
    internal void Remember(nint hwnd, bool included) { lastWindow = hwnd; lastIncluded = included; known = true; }
}
