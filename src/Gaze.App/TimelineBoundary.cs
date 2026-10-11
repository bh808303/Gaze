using System.Drawing;
using Gaze.Core;

namespace Gaze.App;

internal static class TimelineBoundary
{
    internal static bool IncludesWindow(CaptureMode mode, bool recordedRelationship, Rectangle bounds, IReadOnlyList<Rectangle> areas)
        => mode == CaptureMode.Window ? recordedRelationship : areas.Any(area => area.IntersectsWith(bounds));
    internal static bool IncludesPoint(Point point, IReadOnlyList<Rectangle> areas) => areas.Any(area => area.Contains(point));
}
