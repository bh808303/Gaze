using System.Collections.Concurrent;
using System.Drawing;

namespace Gaze.App;

// The system-wide hook is only a producer. Consumers may wait on rendering locks.
internal sealed class ClickQueue
{
    private readonly ConcurrentQueue<(string Button, Point Point, DateTimeOffset Time)> pending = new();
    internal void Enqueue(string button, Point point, DateTimeOffset time) => pending.Enqueue((button, point, time));
    internal void Drain(Action<string, Point, DateTimeOffset> consume)
    {
        // Bound each drain so a busy producer cannot indefinitely delay a frame.
        int count = pending.Count;
        while (count-- > 0 && pending.TryDequeue(out var click)) consume(click.Button, click.Point, click.Time);
    }
}
