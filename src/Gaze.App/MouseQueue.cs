using System.Drawing;

namespace Gaze.App;

internal readonly record struct RawMouse(string Kind, string? Button, Point Point, DateTimeOffset Time,
    long Milliseconds, int Delta = 0, string? Axis = null, bool Held = false);

// Short producer lock protects only queued data, never desktop queries or rendering.
internal sealed class MouseQueue
{
    private readonly object gate = new();
    private readonly Queue<RawMouse> pending = new();
    private readonly HashSet<string> held = [];
    private long? lastMove;
    private Point? lastPoint;
    private int pointers;
    private long lost;
    internal long Lost { get { lock (gate) return lost; } }
    internal void Enqueue(RawMouse input)
    {
        lock (gate)
        {
            if (input.Kind == "click" && input.Button != null) held.Add(input.Button);
            if (input.Kind == "button-release" && input.Button != null) held.Remove(input.Button);
            input = input with { Held = held.Count > 0 };
            if (input.Kind == "pointer")
            {
                if (lastPoint == input.Point || (lastMove.HasValue && input.Milliseconds - lastMove.Value < (input.Held ? 34 : 100))) return;
                lastMove = input.Milliseconds; lastPoint = input.Point;
                if (pointers >= 256) { lost++; return; }
            }
            if (pending.Count >= 4096) { lost++; return; }
            pending.Enqueue(input);
            if (input.Kind == "pointer") pointers++;
        }
    }
    internal void Drain(Action<RawMouse> consume)
    {
        RawMouse[] batch;
        lock (gate) { batch = pending.ToArray(); pending.Clear(); pointers = 0; }
        foreach (var item in batch) consume(item);
    }
    internal void ResetSampling() { lock (gate) { held.Clear(); lastMove = null; lastPoint = null; } }
}
