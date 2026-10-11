using System.Collections.Concurrent;

namespace Gaze.App;

internal readonly record struct RawKey(int VirtualKey, int ScanCode, uint Flags, bool Down, nint Foreground, DateTimeOffset Time);
internal sealed class KeyboardQueue
{
    private readonly ConcurrentQueue<RawKey> pending = new();
    internal void Enqueue(RawKey key) => pending.Enqueue(key);
    internal void Drain(Action<RawKey> consume)
    {
        int count = pending.Count;
        while (count-- > 0 && pending.TryDequeue(out var key)) consume(key);
    }
}
