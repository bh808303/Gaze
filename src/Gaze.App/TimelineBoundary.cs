using System.Drawing;
using Gaze.Core;

namespace Gaze.App;

internal static class TimelineBoundary
{
    internal static bool IncludesWindow(CaptureMode mode, bool recordedRelationship, Rectangle bounds, IReadOnlyList<Rectangle> areas)
        => mode == CaptureMode.Window ? recordedRelationship : areas.Any(area => area.IntersectsWith(bounds));
    internal static bool IncludesPoint(Point point, IReadOnlyList<Rectangle> areas) => areas.Any(area => area.Contains(point));
    internal static bool TryMapMonitorPoint(Point point, IReadOnlyList<Rectangle> areas,
        IEnumerable<(Rectangle Source, Rectangle Destination)> mappings, out int x, out int y)
    {
        x = y = 0;
        if (!IncludesPoint(point, areas)) return false;
        foreach (var (source, destination) in mappings)
        {
            if (source.Width <= 0 || source.Height <= 0 || destination.Width <= 0 || destination.Height <= 0 || !source.Contains(point)) continue;
            x = destination.X + (int)((point.X - source.X) * (long)destination.Width / source.Width);
            y = destination.Y + (int)((point.Y - source.Y) * (long)destination.Height / source.Height);
            return true;
        }
        return false;
    }
    internal static bool IncludesPopup(string className, bool recordedMenuOwner, bool recordedComboList)
        => (className == "#32768" && recordedMenuOwner) || (className == "ComboLBox" && recordedComboList);
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
