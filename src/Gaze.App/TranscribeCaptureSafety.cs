namespace Gaze.App;

// Own resources and event subscriptions as soon as acquisition succeeds.
internal sealed class TranscribeCaptureResources : IDisposable
{
    private readonly Stack<Action> cleanup = new();
    internal void Own(Action release) => cleanup.Push(release);
    internal static TranscribeCaptureResources Acquire(Action<TranscribeCaptureResources> acquire)
    {
        var resources = new TranscribeCaptureResources();
        try { acquire(resources); return resources; }
        catch { resources.Dispose(); throw; }
    }
    public void Dispose()
    {
        // One failing release must not strand later native resources.
        Exception? error = null;
        while (cleanup.TryPop(out var release))
        {
            try { release(); }
            catch (Exception failure) { error ??= failure; }
        }
        if (error != null) throw error;
    }
}

internal static class TranscribeFrameSafety
{
    internal static bool CanPublish(int width, int height, int surfaceWidth, int surfaceHeight)
        => width > 0 && height > 0 && width == surfaceWidth && height == surfaceHeight;
}

internal static class TranscribeFramePump
{
    internal static async Task<T> WaitAsync<T>(Func<Task<T?>> pull, Func<Exception?> failure, int timeoutMilliseconds = 3000) where T : class
    {
        var deadline = Environment.TickCount64 + timeoutMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            if (failure() is { } error) throw error;
            var frame = await pull().ConfigureAwait(false);
            if (frame != null) return frame;
            await Task.Delay(20).ConfigureAwait(false);
        }
        throw new TimeoutException("No fresh screenshot frame arrived within three seconds.");
    }
}
